# Germany: invoicing rules and XRechnung

With Germany turned on, nInvoices checks what § 14 UStG asks of an invoice, prints the Steuernummer, the
USt-IdNr and the note that explains a missing VAT, and generates an **XRechnung 3.0** e-invoice (UBL 2.1
syntax) for any finalized invoice. None of this applies until you choose Germany in
*Settings → Country & taxes → Your country* and turn on its *Invoicing rules*.

Every business in Germany must be able to receive e-invoices since 2025, and issuing them becomes
mandatory between businesses in 2027 or 2028, depending on revenue. Until then nInvoices generates the
XRechnung **on request**: open a finalized invoice and use the *E-invoice* panel. The file is not
signed (XRechnung needs no signature) and nInvoices does not send it: email it, or upload it to the
recipient's portal (a public administration gives you its Leitweg-ID and portal).

## Setting it up

*Settings → Country & taxes → Invoicing rules → Germany*:

| Field | What for |
|---|---|
| Tax ID (the issuer's) | Your Steuernummer, or your USt-IdNr. Required on every invoice. |
| USt-IdNr | When the Tax ID is the Steuernummer. Needed for customers in other EU countries. |
| Kleinunternehmer (§ 19 UStG) | You charge no VAT; the invoice carries the § 19 note. |
| Contact name, phone, email | XRechnung requires a seller contact (BR-DE-2). The email is also the sender's address on the file. |
| IBAN (BIC optional) | XRechnung requires the account for a bank transfer (BR-DE-1). |

On each customer (*Customers → edit → Germany invoicing*):

- **E-invoice email**: where their XRechnung goes. Required.
- **Leitweg-ID / buyer reference**: the Leitweg-ID of a public administration, or the reference a
  business gave you (a purchase order). Left empty, the invoice number is used (XRechnung requires one).
- The customer's **Tax ID** is read as their VAT identifier when it looks like one (two letters, then the
  number), as reverse charge and intra-community supplies need.

### Taxes: which VAT category

A VAT tax above 0% is the standard rate (category S). For a VAT tax **at 0%**, choose why none is charged:

| Category | When |
|---|---|
| AE · Reverse charge | Services to a business in another EU country (§ 13b UStG). Needs the customer's VAT identifier. |
| K · Intra-community supply | Goods to a business in another EU country. Needs both VAT identifiers. |
| G · Export | Goods leaving the EU. Needs your USt-IdNr. |
| O · Not subject | Services not taxable in Germany, e.g. to a business outside the EU. The VAT identifiers are left out of the file, so your Tax ID must then be the Steuernummer. |
| E · Exempt | § 4 UStG: give the legal basis in *Reason on the invoice*. |
| Z · Zero rate | § 12 (3) UStG, e.g. solar panels. |

A Kleinunternehmer needs no VAT tax at all: the invoice is exempt (E) with the § 19 note.

One invoice has one VAT category here (each tax applies to the whole invoice), and XRechnung has no
withholding: a negative tax is refused.

## What is checked before the file is made

nInvoices refuses to build an XRechnung that a recipient would reject for a reason it can see: missing
contact or IBAN, no e-invoice email for the customer, a Kleinunternehmer charging VAT, a 0% VAT without its
category, the VAT identifiers a category needs or forbids, a VAT amount that doesn't match its rate, lines
and VAT that don't add up to the total. The message says what to fill in.

## How it is verified

- **The official schema and rules.** The tests (`XRechnungValidationTests`) validate the generated files,
  for the standard rate, a Kleinunternehmer (with and without USt-IdNr), reverse charge and not subject,
  against the **UBL 2.1** schema and through **KoSIT's validator** with the **EN 16931** and **XRechnung
  3.0.2** Schematron rules: every file must be *accepted*, with no error. The schemas and rules come from
  KoSIT's validator configuration (release 2026-08-31), kept under
  `tests/nInvoices.Application.Tests/Compliance/Germany/Schemas/`.
- **Not yet verified:** sending a file to a real recipient or portal (ZRE, OZG-RE, Peppol). The validator
  still reports one *information* note on every file (BR-DE-TMP-32): it would like a delivery date or a
  billing period, which nInvoices doesn't record yet. It does not cause a rejection.

### Running the validator yourself

The UBL schema test always runs. The KoSIT test needs **Java 11 or later** on the PATH and the validator jar
(10 MB, not committed); without either it is skipped. To fetch the jar (from Maven Central, SHA-256 checked):

```powershell
tests/nInvoices.Application.Tests/Compliance/Germany/get-kosit-validator.ps1
```

then `dotnet test`. `KOSIT_VALIDATOR_JAR` points the test at another jar; CI fetches it and sets
`REQUIRE_KOSIT_VALIDATOR=1`, so there a missing validator fails the build instead of skipping.

To check a file by hand, with the configuration from the repository:

```bash
java -jar validator-1.6.3-standalone.jar -s tests/nInvoices.Application.Tests/Compliance/Germany/Schemas/scenarios.xml -r tests/nInvoices.Application.Tests/Compliance/Germany/Schemas -h invoice.xml
```

On Windows, pipe it an empty input (in Git Bash: `printf '' | java ...`): with standard input on `NUL`
it stops with "Incorrect function".

## Not built yet

- ZUGFeRD / Factur-X (the PDF with the XML inside) and the CII syntax of XRechnung.
- Credit notes (*Gutschrift*, *Rechnungskorrektur*), a delivery date or billing period on the invoice,
  several VAT rates on one invoice.
- Delivery through Peppol or the federal and state portals.

## For developers

Everything lives in `src/nInvoices.Countries.Germany`: `GermanyComplianceModule` (fields and checks),
`XRechnung/XRechnungFormat` (the checks before building, then the file), `XRechnung/XRechnungXmlBuilder`
(the UBL document, in the schema's element order; the comments name the business terms, BT-n, and rules,
BR-n) and `GermanVat` (an invoice's VAT as EN 16931 sees it). When you change the builder, run the
validator test: the schema enforces the element order, the Schematron the rest.
