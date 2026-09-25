# UI backlog: items left over from the September 2026 UI overhaul

The `feature/partial_day` branch delivered the unified time entry (calendar, list and day
panel), the design system, the app shell, the full-page template editor, and the reworked
Customers, Invoices, Dashboard and Settings screens. These items from the original proposal
were not done. They are listed in the suggested order.

## 1. Invoice creation as a guided wizard

Today "New invoice" is one long page with a sticky summary bar. The proposal was four steps:

1. **Customer & period.** Customer, invoice type, issue date, month, timesheet template.
2. **Time.** The existing calendar / list / day panel.
3. **Expenses.**
4. **Review & generate.** A preview of the invoice PDF and the timesheet, rendered with the
   real data before anything is saved. The preview endpoints added for the template editor
   (`POST /api/invoicetemplates/preview`, `POST /api/monthlyreporttemplates/preview`) render
   *sample* data; this step needs a variant that takes the `GenerateInvoiceDto` being built.

Where to start: `src/nInvoices.Web/src/views/invoices/InvoiceGenerate.vue` (split its three
panels into step components) and `TemplatePreviewService` in `nInvoices.Application/Services`.

**Status: done on `feature/invoice-wizard`.** The page is a stepper over
`components/invoices/wizard/Step*.vue`; state and per-step rules live in
`composables/useInvoiceDraft.ts`. One-time invoices skip the Time step. The review step calls
`POST /api/invoices/preview` (`PreviewInvoiceDraftQuery`), which runs the real generation path
(`IInvoiceGenerationService.PreviewInvoiceAsync`, `IMonthlyReportGenerationService.PreviewReportHtmlAsync`)
without advancing the sequence, saving work days or creating projects. Project names in the
preview keep the typed spelling (projects are matched and created only on generate).

## 2. Autosaved invoice drafts

A refresh or an accidental navigation away from "New invoice" loses the month you entered.
Keep the in-progress `GenerateInvoiceDto` (customer, period, work days, expenses) per customer
and month, restore it when the page opens again, and clear it after a successful generate.
Browser storage is enough for a single user; server-side drafts would also work across devices.
Warn before leaving the page with unsaved entries, the same way the template editor does.

## 3. Week view for multi-project customers

A third view next to Calendar and List: projects as rows, the days of one week as columns,
one hours cell per project and day, and a notes row. Show it only when the customer has more
than one active project or bills hourly. It can edit the same data through `useWorkMonth`
(`src/nInvoices.Web/src/composables/useWorkMonth.ts`), like the other two views.
The mock is in the proposal page ("Proposal C · Week grid").

## 4. Holiday calendar per country

Pre-filling the month marks every weekday as worked. Public holidays are still marked by hand.
A per-country holiday list (from the customer's or the freelancer's country) could mark them
as "Public holiday" automatically when a month is opened.

**Status: done.** `HolidayCalendar` / `HolidayRule` (fixed date, Easter ± days, nth weekday of a
month, optional year range) are created per country from `BuiltInHolidays` (IT, DE, AT, FR, ES,
GB, US; national holidays only, no weekend substitute days) on first use and edited in Settings →
Public holidays ("Reset to built-in" restores them). A customer uses `HolidayCountry` when set,
otherwise its address country matched by name (`CountryCodes`). The wizard marks holidays on
untouched weekdays when the month is filled or the customer changes (`useInvoiceDraft`).
Not done: local holidays (a city's patron saint) as their own feature — they can be added as
rules, but then apply to every customer in the country; holiday calendars are not in the JSON
export/import.

## 5. Command palette and keyboard shortcuts

`Ctrl+K` to jump anywhere or start an action ("new invoice for Nea srl", "open 26-09-005").
The calendar already has keys (W, H, L, Del, 1–8, arrows); the rest of the app has none.

## 6. Invoice list at scale

Fine for today's volumes. With hundreds of invoices it needs pagination (server-side), saved
filters, sorting by column, and bulk actions (mark several as paid, download several PDFs).

## 7. Housekeeping

- `src/nInvoices.Web/src/components/UserMenu.vue` is no longer used (the header's user menu
  replaced it) and can be deleted.
- The invoices store still exposes getters that no screen uses (`invoiceCount`,
  `draftInvoices`, `totalRevenue`); remove them or use them.
- `npm audit` reports vulnerabilities in `axios` and `vite` and packages they pull in, and
  `dotnet build` warns about `Microsoft.OpenApi` 2.0.0 and `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.
  Upgrade them in a separate change.
- One Application test (`RenderAsync_WithFormatCurrencyFunction_FormatsCorrectly`) fails on
  machines whose regional format uses a decimal comma: the test depends on the machine's
  culture. Pin the culture in the test.
- Consider tagging Docker images by commit (not only `:latest`) so a deploy can be rolled back.
