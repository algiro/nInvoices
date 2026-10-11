# Tax prediction

nInvoices can estimate the taxes you will pay each quarter, from the invoices you issue and the
business expenses you record, so you can put the money aside in time. It is **an estimate to plan
ahead**: it files nothing and pays nothing, and it is not tax advice. Your gestor's or tax adviser's
figures can differ, for example because of expenses or income that aren't in nInvoices.

It is built in for **Spain**, for an autónomo in *estimación directa simplificada*:

- **Modelo 130**, the quarterly income-tax prepayment (*pago fraccionado del IRPF*);
- **Modelo 420** (IGIC, Canary Islands) or **Modelo 303** (IVA, mainland and Balearic Islands).

## Setting it up

1. **Settings → Taxes**: choose the country, the regime and where you pay indirect tax (Canary Islands
   or elsewhere). The two options below start ticked; untick them to match what your gestor does
   (see [The settings](#the-settings)). Save.
2. **Your taxes on invoices need nothing special.** A positive tax counts as the indirect tax of your
   territory (IGIC in the Canary Islands, IVA elsewhere), and a withholding such as IRPF is simply a tax
   with a negative rate (e.g. −15%). Only if you charge both, IVA to some customers and IGIC to others,
   say which one each tax is: turn on Spain in *Settings → Invoicing rules by country* and choose
   *Which tax is this?* on the tax (*Customers → the customer → Taxes*). A tax marked as the other one
   is left out of your return, with a warning.
3. **Record your expenses** in *Expenses* (below), including the social security fee.

Then open **Tax prediction**.

## Expenses

*Expenses* holds the costs of your business, the ones you deduct from your income. They are not the
expenses you bill to a customer: those go on the customer's invoice.

- **One-off**: an invoice from a supplier. Enter the amount **without** IVA/IGIC, and the IVA/IGIC you
  paid separately: it is deducted from your Modelo 420/303.
  - **Reverse charge**: tick it for a supplier abroad that charged no IVA/IGIC (a US software company,
    say). You charge the tax to yourself and deduct it in the same return, so it adds nothing to pay,
    but the expense still counts for Modelo 130.
  - **Business use**: the share that is a business cost (e.g. 30% of a home office); 100% by default.
- **Recurring**: something you pay every month, such as the **social security fee** (*cuota de
  autónomo*). It counts in every month from *From* to *Until* (empty while it goes on). When the
  amount changes, set *Until* on the old one to the month before and add a new one from the month it
  changes.

## Reading the page

For each quarter, each form shows:

| | |
|---|---|
| Status | *Quarter in progress* (the estimate still changes), *Due* (until the deadline), *Overdue*, *Paid* |
| Estimated | what the rules give from what is recorded |
| Paid | what you recorded, and the difference from the estimate |
| How | the form's boxes, so you can compare them with your gestor's form line by line |

The tiles at the top add up the year, what you have paid and the next form to pay. Warnings above the
quarters say what was left out (see [What is not counted](#what-is-not-counted)).

**Record payment** saves what you actually paid (the form is filled in with the estimate). Do it
every quarter: Modelo 130 is cumulative, and the next quarters start from what you paid, not from the
estimate. A quarter with nothing to pay is recorded as 0.

## How it is worked out (Spain)

### Modelo 130

Cumulative: each quarter looks at the whole year so far, from 1 January to the end of the quarter.

| Box | |
|---|---|
| 01 | Income invoiced since 1 January: the invoices' amounts before taxes (including expenses billed on them) |
| 02 | Deductible expenses since 1 January: your expenses (by their business share) and recurring expenses, plus the 5% for hard-to-justify expenses if ticked |
| 03 | Net income: 01 − 02 |
| 04 | 20% of 03 (0 if 03 is negative) |
| 05 | What was paid for Modelo 130 in the earlier quarters of the year (the recorded payment, or the estimate if none was recorded) |
| 06 | Income tax your customers withheld since 1 January, if ticked |
| 07 | Result: 04 − 05 − 06. A negative result means nothing to pay |

**Hard-to-justify expenses** (*gastos de difícil justificación*): 5% of the net income before them
(7% in 2023), at most €2,000 a year.

### Modelo 420 / 303

Each quarter on its own: the IGIC (or IVA) charged on the quarter's invoices, plus the tax
self-charged on reverse-charge expenses, minus the tax you can deduct on the quarter's expenses. A
negative result is not paid: it is carried as a credit into the next quarters.

### Deadlines

20 April, 20 July and 20 October for the first three quarters, 30 January of the next year for the
fourth. A deadline on a Saturday or Sunday moves to the Monday; national and regional holidays are not
taken into account.

### An example

A quarter (the first of the year) with €12,000 invoiced to a business in another EU country (reverse
charge, no IGIC), €3,000 to a Canarian business (+7% IGIC, −15% IRPF), €400 of expenses and a social
security fee of €300 a month:

| | |
|---|---|
| 01 Income | 15,000.00 |
| 02 Expenses | 1,300.00 + 685.00 (5% of 13,700.00) = 1,985.00 |
| 03 Net income | 13,015.00 |
| 04 20% | 2,603.00 |
| 06 Withheld | 450.00 |
| **Modelo 130** | **2,153.00** (or 2,603.00 with *Subtract what customers withheld* unticked) |
| **Modelo 420** | **210.00** (7% of 3,000) |

## The settings

| Setting | |
|---|---|
| Where you pay indirect tax | Canary Islands: IGIC and Modelo 420. Elsewhere: IVA and Modelo 303 |
| Deduct hard-to-justify expenses | Most gestores apply it. Untick it if yours doesn't |
| Subtract what customers withheld in Modelo 130 | The law allows it (box 06). Some gestores leave it out and the withholdings come back in the annual return; untick it to match them |

## What is not counted

- **Draft and cancelled invoices.** An invoice counts from when it is finalized, by its issue date.
- **Invoices and expenses in other currencies than the euro.** Spain taxes in euros at the exchange
  rate of the day; converting them is not done yet. The page says how many were left out.
- **Income or expenses outside nInvoices**: other activities, rents, an expense you didn't record.
- **The annual returns** (Renta, Modelo 390/425), the *módulos* regime, Ceuta and Melilla (IPSI) and
  companies.

The calculation was checked against a real autónomo's quarters in the Canary Islands: Modelo 420
exactly and Modelo 130 to the cent for the first quarter. A difference in the second quarter came from
an expense that wasn't recorded, which is the most common reason for a gap.

## Your data

Expenses and payments are stored encrypted, like the rest of your data (see
[ENCRYPTION.md](ENCRYPTION.md)), are included in the backup with your settings, and are deleted with
your account.

## For developers: another country

A country is an `ITaxPredictionModule` (`src/nInvoices.Core/TaxPrediction`): its regimes and their
settings (the same field descriptors as the e-invoicing modules), the currency it taxes in, how to tell
which indirect tax a tax is, and a pure `Predict` from plain income, expenses and payments to periods,
forms and their lines. Register it in `TaxPredictionExtensions.AddTaxPrediction`; the API and the UI
need nothing else. The Spanish module, its calculators and `SpainTaxPredictionTests` (under
`src/nInvoices.Application/TaxPrediction/Spain` and its tests) are the example to follow.
