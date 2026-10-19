# English launch posts (drafts)

Replace `<LINK-DEMO>` and `<GIF>`. Before posting anywhere:
- Read the community's current self-promotion rules. r/selfhosted has restricted new-project posts and asks
  for disclosure when AI tools helped write the code (the commit history shows Claude co-authorship): check
  the rules and disclose if they require it.
- Say "source-available" in the first lines. Never call it open source.
- Stay around for the first hours to answer comments.

---

## 1. r/selfhosted

**Title:** nInvoices: self-hosted invoicing and timesheets for freelancers who bill by the day (source-available, Docker)

I bill clients by the day, and the monthly routine (count worked days, subtract holidays, work out
taxes, export the PDF, email it) was a spreadsheet and a Word template. So I built a tool around that routine.

**What it does**
- Mark worked days on a calendar (public holidays are pre-marked), see the **real invoice and timesheet
  before anything is saved**, then generate the PDF.
- Your own HTML templates ([Scriban](https://github.com/scriban/scriban)), per-customer language, daily/hourly/monthly
  rates, several currencies, expenses, taxes as rules (percentage, fixed, compound).
- Invoice lifecycle (draft, finalized, sent, paid, cancelled), dashboard, bulk actions, optional Gmail drafts.
- Optional per-country rules, off by default: Spain (signed Facturae, FACe, Verifactu), Italy (FatturaPA),
  Germany (XRechnung), UK (VAT checks). Plus tax estimates for each.

**Self-hosting**
- .NET 10 API + Vue 3 frontend. SQLite for a laptop, PostgreSQL for a server.
- `docker compose` stack with Keycloak (OIDC, optional Google sign-in), or a no-login mode for local use.
- Multi-user: each user has their own data, and new accounts need admin approval.
- Customer data, amounts and templates are **encrypted at rest** with one key per user; backup is a single
  passphrase-encrypted file you can open without the app (`tools/decrypt-backup.mjs`). Users can delete
  their account, which destroys their key.
- OpenTelemetry out of the box, health endpoints, Dependabot, CI that builds both images.

**Honest notes**
- The license is **PolyForm Shield (source-available)**: free for any use including your own business,
  read and modify the code, but you can't offer it as a competing product or service. Not OSI open source.
- The Spanish, Italian and German e-invoicing passes the official schemas/validators but **hasn't been run
  through the real government services yet**. Treat it as a preview.
- It is a one-person project.

Don't want to set anything up? I can give you free access to my instance (<LINK-DEMO>) to look around.
It's a test instance with sample data and no uptime guarantee.

GitHub: https://github.com/algiro/nInvoices

Feedback on the Docker setup and what you'd want from it is very welcome.

---

## 2. Show HN

**Title:** Show HN: nInvoices – self-hosted invoicing for freelancers who bill by the day (source-available)

**First comment (post it right after submitting):**

Hi HN. I freelance and bill by the day, and the monthly invoice (count worked days, subtract holidays,
taxes, PDF, email) was a spreadsheet plus a Word template. nInvoices is built around that routine: you
mark days on a calendar, it shows the actual invoice and timesheet before saving anything, then renders
the PDF from your own HTML template.

Where it differs from the usual invoicing tools is country rules, as optional plug-ins: Spain (signed
Facturae, FACe, Verifactu hash chain), Italy (FatturaPA), Germany (XRechnung, validated with KoSIT), UK
(VAT checks), plus quarterly tax estimates for each. I'm upfront about the gaps: the files pass the official
schemas, but I haven't been able to test against AEAT/FACe/SDI/portals yet, and I'd love help with that.

Stack: .NET 10 (Clean Architecture, source-generated Mediator), Vue 3, EF Core on SQLite/PostgreSQL,
PuppeteerSharp for PDFs. Sensitive fields are encrypted per user, so a database copy alone is unreadable.

License: PolyForm Shield, so source-available rather than open source: free to use and self-host, not to
resell as a competing service. I know that's a trade-off; happy to explain the reasoning.

Repo: https://github.com/algiro/nInvoices. If you'd rather click around first: <LINK-DEMO> (sample data).

Posting tips: weekday, roughly 8-10am US Eastern; link the repo (not a landing page) unless the landing
page has a live demo; don't ask for upvotes anywhere.

---

## 3. dev.to / Hashnode article

**Title:** Country rules as plug-ins: how I added Spain, Italy, Germany and the UK to a .NET invoicing app

**Outline**
1. *The problem.* Same app, four countries, four sets of invoicing rules. Hard-coding `if (country == ...)`
   everywhere would not survive the second country.
2. *The seam.* Core/Application never name a country. They expose hooks: `ICountryComplianceModule`,
   `IEInvoiceFormat`, `IEInvoiceChannel`, `IInvoiceLifecycleStep`, `IInvoiceTemplateModelContributor`,
   `ITaxPredictionModule`. Show the table from the README and one small module (Germany).
3. *Enforcing it.* `ArchitectureTests` fails the build if shared code references a country, and
   `SupportedCountriesTests` fails when a country is missed in a list.
4. *A hash-chained, append-only log in EF Core* (Verifactu): `IAppendOnlyEntity`, the DbContext refusing
   updates and deletes, and a check that detects a missing or altered record.
5. *Testing against official artifacts.* Schemas for Facturae/FatturaPA/UBL, KoSIT's validator in CI,
   and why "passes the schema" is not the same as "accepted by the portal" (the honest limit).
6. *Data per user.* Global query filters on an owner id, per-user encrypted columns, and the catch:
   never filter or sum encrypted columns in SQL.
7. *Closing.* Link the repo, name what's unverified, ask for testers per country.

Tags: `dotnet`, `csharp`, `vue`, `architecture`. Add the architecture diagram from the README as the cover.
