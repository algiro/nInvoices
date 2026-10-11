# Implementation plan — Tax prediction

Status: implemented 2026-10-11 on `feature/tax-prediction`, all five phases. The user guide is
[TAX-PREDICTION.md](TAX-PREDICTION.md); this file records how it was planned and built.

## Goal

A new **Tax prediction** section estimates the taxes a freelancer will pay for each period, from the
invoices already in nInvoices plus the business expenses they record. The rules depend on the
country where the user pays taxes, the tax regime they chose, and settings of that country.

The first country is **Spain**, for an *autónomo* in *estimación directa simplificada*, who pays each
quarter:

- **Modelo 130**, the income-tax prepayment (*pago fraccionado del IRPF*), and
- **Modelo 420** (IGIC, Canary Islands) or **Modelo 303** (IVA, mainland).

This is an **estimate to plan cash**, not a tax return: the page says so, and it shows every line
(the form's boxes) so the user can compare it with what their gestor files.

Out of scope for now: the annual forms (Renta, Modelo 390/425), Modelo 349, the *módulos* regime,
Ceuta/Melilla (IPSI), companies (Impuesto de Sociedades), other countries.

## Checked against real data (2026)

The rules were checked against a real autónomo's first two quarters of 2026 in the Canary Islands
(the figures are kept private): reverse-charge invoices to an EU client, Canarian invoices with 7%
IGIC and a 15% withholding, a few expenses and the social security fee. Modelo 420 matched exactly,
Modelo 130 matched the first quarter to within €0.50, and the second quarter's gap was an expense that
hadn't been recorded.

The rules that reproduce what that gestor filed:

- 20% of the year-to-date net income, minus the previous 130 payments of the year;
- the **5% deduction for hard-to-justify expenses** (*gastos de difícil justificación*), at most
  €2,000 a year, applied;
- the **client withholdings not subtracted** (casilla 06 left at 0). The official rule subtracts
  them; some gestores don't (they come back in the annual return). Hence a setting.

The acceptance tests use a made-up half-year of the same shape (see `SpainTaxPredictionTests`).

## Key decisions

| Topic | Decision | Why |
|---|---|---|
| Where it lives | A new kind of country module, `ITaxPredictionModule`, next to (not inside) `ICountryComplianceModule` | Paying taxes in Spain doesn't imply issuing Spanish e-invoices, and vice versa. Same pattern: registered in the container, found by country code, nothing outside it knows country rules. |
| Settings | One `TaxProfile` per user: country, regime, module values (`Dictionary<string,string>`, like `ComplianceSettings.Values`) | Same `ComplianceField` descriptors drive the settings form, so the UI is generic. |
| Business expenses | A **new** entity, `BusinessExpense`. The existing `Expense` is a cost billed *to a customer* on an invoice. | Different meaning: deductible costs of the freelancer, with or without a customer. |
| Social Security fee | A `RecurringExpense` (amount per month, from/to month), not a tax setting | It is a deductible expense; an autónomo can change the fee (e.g. a higher one from July). Generic for other countries' contributions. Expanded in memory, never materialised as rows. |
| Real payments | `TaxPayment` rows (year, period, form, amount, paid on) | Modelo 130 is cumulative: it subtracts what was *paid* before, so once a real payment is recorded the next quarter starts from it, not from our estimate. Also shows estimate vs paid. |
| Which invoices | Finalized, Sent and Paid, by **issue date** (*criterio de devengo*); Draft and Cancelled excluded | What Spanish law uses; confirmed by the user. Drafts can be shown separately as "if issued". |
| Classifying tax lines | Negative rate = withholding; positive rate = IGIC or IVA from the tax's `ES.taxType`; 0% = nothing to pay | Same rule as `EInvoiceDocumentFactory`; extract it to one shared `TaxLineClassifier` used by both. |
| Calculation | Pure functions in Application with plain inputs, no EF | Testable with plain figures, like `InvoiceCalculator`. |
| Encryption | Every amount, description and supplier is `.IsEncrypted(...)` | Same rule as the rest of the data (`Docs/ENCRYPTION.md`); all sums happen in memory anyway. |
| Currency | v1 counts only invoices and expenses in **EUR**; others are listed as "not counted" with a warning | Spain taxes in euros at the exchange rate of the date; conversion is a later step (open question 1). |

## Domain model (Core)

```
TaxProfile : OwnedEntityBase          one per user (unique OwnerId)
  CountryCode   "ES"
  Regime        "ES.eds"   (estimación directa simplificada)
  Values        { territory: "canarias" | "mainland",
                  difficultExpenses: "true",
                  deductWithholdings: "false" }

BusinessExpense : OwnedEntityBase
  Date            DateOnly
  Description     string   (encrypted)
  Supplier        string?  (encrypted)
  Amount          Money    (encrypted)  taxable base, without IGIC/IVA
  IndirectTax     Money?   (encrypted)  IGIC/IVA paid, deductible on 420/303
  ReverseCharge   bool     foreign supplier that charged no IGIC/IVA (e.g. US software):
                           self-charged and deducted in the same 420/303, net 0
  Category        enum     SocialSecurity, ProfessionalServices, Software, Equipment, Office, Travel, Other
  DeductiblePercent decimal  100 by default (e.g. 30 for a home office)

RecurringExpense : OwnedEntityBase
  Description, Category, Amount (encrypted), FromMonth (yyyy-MM), ToMonth? (yyyy-MM)
  → "Social Security fee  €300.00  2026-01 … 2026-06",  "€500.00  2026-07 …"

TaxPayment : OwnedEntityBase
  Year, Period (1-4), Form ("130", "420", "303"), Amount (encrypted), PaidOn DateOnly
  unique (OwnerId, Year, Period, Form)
```

Ports in `Core/TaxPrediction/`:

```csharp
public interface ITaxPredictionModule
{
    string CountryCode { get; }
    IReadOnlyList<TaxRegime> Regimes { get; }                       // id, name, its fields
    TaxYearPrediction Predict(TaxPredictionInput input);            // pure
}

TaxPredictionInput: year, profile values, today,
                    IncomeItem[]   (date, base, indirect tax kind+rate+amount, withheld),
                    ExpenseItem[]  (date, base, indirect tax, reverse charge, category, deductible %),
                    PaymentItem[]  (period, form, amount)
TaxYearPrediction:  Period[] → Form[] → { code, name, deadline, status (estimate/paid),
                                          estimated, paid?, Line[] (box, label, amount) , warnings }
```

## Spain module (Application/TaxPrediction/Spain)

`SpainTaxPredictionModule` with one regime, `ES.eds`, and these fields:

| Key | Type | Default | Meaning |
|---|---|---|---|
| `territory` | Choice: `canarias`, `mainland` | — (required) | IGIC/Modelo 420 or IVA/Modelo 303 |
| `difficultExpenses` | Boolean | ticked (regime default) | 5% of the net income, max €2,000 a year |
| `deductWithholdings` | Boolean | ticked (regime default) | Subtract client withholdings in Modelo 130 (box 06). Some gestores leave it off. |

`Modelo130Calculator` (pure), per quarter q, with year-to-date sums up to the end of q:

```
01 income            Σ invoice bases (subtotal + billed expenses)
02 expenses          Σ expense bases × deductible % + recurring months
   difficult (5%)    min(5% × (01 − 02), 2000)          if enabled
03 net               01 − 02 − difficult
04 20% of 03         max(0, 0.20 × 03)
05 previous          Σ Modelo 130 of earlier quarters: the paid amount if recorded, else our estimate
06 withholdings      Σ withheld on invoices, if enabled
07 result            04 − 05 − 06   (a negative result is 0 to pay)
```

`Igic420Calculator` / `Iva303Calculator` (pure), per quarter on its own (not cumulative):

```
output tax           Σ IGIC/IVA charged on invoices, by rate
reverse charge       self-charged on reverse-charge expenses at the territory's general rate (IGIC 7%, IVA 21%)
input tax            Σ IndirectTax of expenses × deductible %  +  the same self-charged amount
result               output + self-charged − input
```

Deadlines: Q1 20 April, Q2 20 July, Q3 20 October, Q4 30 January of the next year. Rates and limits
(20%, 5%, €2,000, 7%, 21%) are constants of the module with the year they apply from, so a change of
law is one new row.

## Application and API

As built in Phase 3:

- `TaxPrediction/`: `ITaxPredictionRegistry` (modules registered in `AddTaxPrediction()`), and
  `TaxPredictionInputBuilder`, the pure mapping from entities to the module input: issued invoices →
  income (subtotal + billed expenses; tax lines with a negative rate are withholdings, the others are
  classified by the module from the tax's country values), one-off and recurring expenses → deductible
  expenses, payments → recorded payments. Amounts in another currency than the module's are left out
  with a warning.
- `Features/TaxPrediction/`: `GetTaxPredictionQuery(year)` (not set up without a profile; warns about
  drafts of the year), `GetTaxPredictionSettingsQuery` (countries, regimes with fields and `Defaults`,
  the profile), `UpdateTaxProfileCommand` (unknown country/regime → 400; missing required field, choice
  not offered or yes/no not "true" → `ComplianceValidationException` with the fields),
  `RecordTaxPaymentCommand` (upsert per year, period and form, in the module's currency; needs a
  profile), `DeleteTaxPaymentCommand`.
- `Features/BusinessExpenses/`: list by year, create, update, delete, for one-off and recurring expenses.
- Validators for every DTO, wrapped by command validators (`CommandValidatorsTests`).
- Shared with e-invoicing: `InvoiceTaxLine.IsWithholding` and `SpainComplianceModule.IsIgic`.
- Mappers: `TaxPredictionMapper`, `BusinessExpenseMapper` (field descriptors via `ComplianceMapper`).
- API: `GET /api/tax-prediction/{year}`, `GET /api/tax-prediction/settings`, `PUT /api/tax-prediction/profile`,
  `PUT /api/tax-prediction/payments`, `DELETE /api/tax-prediction/payments/{id}`;
  `GET|POST /api/business-expenses` (`?year=`), `PUT|DELETE /api/business-expenses/{id}`, and the same under
  `/api/business-expenses/recurring`. `IInvoiceRepository.GetIssuedInYearAsync` loads the year's issued
  invoices with their tax lines.

## Persistence

- Four configurations in `Infrastructure/Data/Configurations/`, encrypted columns as above,
  per-user unique indexes including `OwnerId`.
- Migrations for **both** providers (`MigrationsUpToDateTests`), and the PostgreSQL test run.
- `DataPortability`: a `TaxPredictionPortability` part (profile, expenses, recurring, payments) so
  backups include them; extend `ImportExportRoundTripTests`.
- `AccountDataEraser`: delete the four tables.

## Frontend

As built in Phase 4:

- Navigation: **Expenses** (`/expenses`) and **Tax prediction** (`/tax`).
- `/tax` (`views/tax/TaxPrediction.vue`): year picker; tiles for the year's estimate, what was paid and
  the next form to pay; the warnings; one panel per quarter with a row per form (`components/tax/TaxFormRow`):
  status (in progress / due / overdue / paid), estimate, payment and its difference, "How" with the
  form's boxes, and "Record payment" (`RecordPaymentForm`, prefilled with the estimate; editing it can
  also delete the record). Not set up → a pointer to Settings. A disclaimer under it.
- `/expenses` (`views/expenses/Expenses.vue`): tabs One-off (by year, totals per currency) and Recurring;
  add/edit in a dialog (`components/expenses/BusinessExpenseForm`, `RecurringExpenseForm`, months as
  `<input type="month">`), delete with confirmation.
- Settings → **Taxes** (`components/settings/TaxProfilePanel`): country, regime, the regime's fields
  through `ComplianceFieldInput`; before the first save the form shows the regime's `Defaults`; the
  server's per-field issues show on their field.
- `api/taxPrediction.ts`, `api/businessExpenses.ts` (no store: each is used by one view);
  `utils/taxPrediction.ts` holds the pure parts (form status, totals, next due, initial values, month
  inputs), tested in `taxPrediction.test.ts`.

## Phases

1. **Calculator first.** Core ports, `SpainTaxPredictionModule`, the three calculators, and
   `SpainTaxPredictionTests` with a made-up half-year shaped like the real one: Q1 Modelo 130, Q2
   starting from a recorded Q1 payment, IGIC per quarter; plus cases for each setting, the €2,000 cap, a negative 130,
   reverse-charge expenses netting to 0, mainland IVA. No database or UI yet.
2. **Persistence**: entities, configurations, both migrations, portability, eraser.
3. **Application + API**: queries/commands/validators, controllers, OpenAPI snapshot, handler tests.
4. **Frontend**: Tax prediction page, Expenses page, Taxes settings panel; `npm run check`.
5. **Docs**: `Docs/TAX-PREDICTION.md` (how it calculates, settings, limits), README feature list.

Each phase is one PR.

## Open questions

1. **Invoices in other currencies.** Convert at the ECB rate of the issue date (needs a rate source),
   let the user type the EUR amount, or keep "not counted"? v1 keeps "not counted".
2. **Billed expenses on invoices** (the existing `Expense`): income in box 01 (assumed), and is the
   cost also a deductible `BusinessExpense`? Or are they *suplidos* (pass-through, neither)?
3. **Credit notes**: there is no rectifying-invoice type today; a cancelled invoice simply drops out.
   Fine for v1?
4. **Q4 and the yearly view**: show the expected annual result (Renta) too, where the withholdings
   left out of Modelo 130 come back? Later phase.
5. **Expenses from receipts**: attach the PDF of each expense (stored like `ImageAsset`)? Useful for
   the gestor; not needed for the estimate.
