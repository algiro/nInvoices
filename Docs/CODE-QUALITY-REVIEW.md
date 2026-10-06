# nInvoices — Source Code Quality Review

_Reviewed 2026-10-05 against `main` @ `c7169f0`. Scope: backend (`src/nInvoices.*`), frontend (`src/nInvoices.Web`), tests, build and repository hygiene._

## Summary

The project has a sound overall shape: Clean Architecture with the correct dependency direction, CQRS through MediatR, a real domain model with value objects (`Money`, `InvoiceNumber`), strong multi-tenant isolation in `ApplicationDbContext`, column encryption, and a sizeable backend test suite (~11k lines). The code is well commented, and the comments explain intent.

The problems sit at the edges of that architecture. Several rules the design implies (validation, exception-to-HTTP mapping, encapsulated entities, thin controllers) are defined but not enforced. Some code has been superseded and never removed. The build has no automated quality gate. The findings below are ordered by impact.

| # | Area | Severity | Effort |
|---|------|----------|--------|
| 1 | FluentValidation validators are never executed | **High** | S — ✅ done |
| 2 | Templates validated with Handlebars but rendered with Scriban | **High** | S — ✅ done |
| 3 | Invoice state machine has gaps; entity setters bypass it | **High** | M — ✅ done (Invoice) |
| 4 | No global error handling; exceptions used as an untyped protocol | **High** | M — ✅ done |
| 5 | Controllers bypass the Application layer (DbContext, service locator) | Medium | M — ✅ done |
| 6 | Two schema-evolution mechanisms (EF/SQLite vs hand-written PG SQL) | Medium | M — ✅ done |
| 7 | Sync-over-async in the PDF path | Medium | S |
| 8 | Dead and duplicated code | Medium | S |
| 9 | Oversized classes and components | Medium | M–L |
| 10 | No CI, no shared build settings, no analyzers | Medium | S |
| 11 | Frontend: no type-checking of `.vue` files, no lint, no tests | Medium | M |
| 12 | Inconsistent time source (`DateTime.*` vs `TimeProvider`) | Low | S |
| 13 | Package hygiene | Low | S |
| 14 | Repository hygiene (root clutter, stale docs) | Low | S |

---

## 1. FluentValidation validators are never executed — High — ✅ Done (2026-10-05)

> **Resolution.** `ValidationBehavior` (Application/Behaviors) runs each request's validators
> before its handler; `AddApplicationRequests()` wires MediatR, the behavior and the validators.
> Each of the 14 commands carrying a validated DTO has a command validator delegating to the DTO
> validator. `ValidationExceptionFilter` (Api) returns failures as a 400 `ValidationProblemDetails`
> with an extra `error` string, so the frontend's `errorMessage()` shows them unchanged. Invoice
> preview validates in its handler and returns the problems in its `errors` list instead of a 400.
> Three rules that had never run contradicted the app and were fixed before enabling:
> `CreateTaxDto.TaxId` is optional (the UI never sends one; the handler derives it), monthly
> `WorkDays` must be present but may be empty (fixed monthly rate), and the rendered-content limit
> went from 50,000 to 10,000,000 characters (rendered invoices embed images as base64).
> `CommandValidatorsTests.EveryRequestCarryingAValidatedDto_HasAValidator` guards against new
> commands skipping validation. Still open: removing the handler checks the validators now cover.

There are **18** `AbstractValidator<T>` classes (in 15 files). `Program.cs` registers them with `AddValidatorsFromAssembly`, but:

- no `IPipelineBehavior<,>` (validation behavior) exists,
- no handler or controller injects `IValidator<T>`,
- `FluentValidation.AspNetCore` auto-validation is not used.

So every validation rule in `Features/**/Validators` is dead code. Invalid input reaches the handlers, which partially re-validate with ad-hoc `throw new ArgumentException/InvalidOperationException` (90 + 75 throw sites). The rules that matter are therefore scattered, duplicated, and in places missing.

**Recommendation**
- Add a `ValidationBehavior<TRequest,TResponse>` MediatR pipeline behavior that runs all `IValidator<TRequest>` and throws a single `ValidationException`. Map that exception to `400 ValidationProblemDetails` (see §4).
- Write one test per validator and one test proving the behavior is wired into the pipeline, so this can't silently regress.
- After that, remove the ad-hoc argument checks from handlers that the validators now cover.

## 2. Templates validated with one engine, rendered with another — High — ✅ Done (2026-10-05)

> **Resolution.** The save-time check was worse than a grammar mismatch. Templates are written with `[[ ]]`, which
> the Handlebars check never looked at (it counted raw `{{`/`}}`), so it validated **nothing** in real templates: the
> two default templates contain 52 `[[ ]]` expressions and no `{{`. A broken `[[ for ]]` saved and only failed when an
> invoice was generated, while valid CSS such as `@media print{.a{b:c}}` was rejected for unbalanced braces.
> Invoice templates are now checked by the command validators (`TemplateSyntaxRules.MustBeValidTemplate`, which calls
> `ScribanTemplateRenderer.ValidateAsync`, the same check the editor uses), so errors come back through the
> validation pipeline as a 400 with line and column. `ITemplateEngine`, `HandlebarsTemplateEngine`,
> `TemplateEngineExtensions` and their 25 tests are deleted; `LocalizationService` is now registered by
> `AddApplicationServices`. Monthly-report and email templates turned out not to be checked on save at all (only
> by the editor's validate endpoint, and email templates not even there): their create/update now go through
> Mediator commands (`Features/MonthlyReportTemplates`, `Features/EmailTemplates`) whose validators apply the same
> rule (`Validation/TemplateSyntaxRules`; email subject and body are each named in the error). Their other actions
> (list, delete, activate) still use repositories in the controllers (§5).

`CreateInvoiceTemplateCommandHandler` and `UpdateInvoiceTemplateCommandHandler` validate content with `ITemplateEngine` → `HandlebarsTemplateEngine` (Core interface, Infrastructure implementation). Rendering, previews, emails and `ValidateTemplateCommand` all use `ITemplateRenderer` → `ScribanTemplateRenderer`.

The two engines have different grammars. A valid Scriban template (`{{ for x in items }}`, pipes, `if/else`) can be rejected on save, or a broken one accepted and only fail at invoice generation. The test suite still covers `HandlebarsTemplateEngine` (`HandlebarsTemplateEngineTests.cs`, 334 lines), which hides the problem.

**Recommendation**: use `ITemplateRenderer.ValidateAsync` in both handlers. Then delete `ITemplateEngine`, `HandlebarsTemplateEngine`, `TemplateEngineExtensions` and their tests. One template language, one validator.

## 3. Invoice lifecycle is not enforced by the aggregate — High — ✅ Done for `Invoice` (2026-10-05)

> **Resolution.** The rules existed three times and disagreed: the bulk handler's `WhyNot` and the UI menu
> refused to pay or cancel a draft, the entity (used by the single-invoice endpoints) allowed it, and
> Paid → Paid / Cancelled → Cancelled. Now `Core/Entities/InvoiceLifecycle.cs` is the only table; `Invoice`
> enforces it and the bulk handler delegates to it. `Status`, `Number`, `Subtotal`, `TotalExpenses`,
> `TotalTaxes` and `Total` have private setters, changed through `Finalize(number)`, `RenumberDraft`,
> `AddExpenses`/`AddTaxes` and `RestoreImported` (import only). The entity no longer stamps `UpdatedAt`
> (the DbContext does on save). `InvoiceLifecycleTests` (52 cases) covers every status × action pair; the
> entity had no tests before. Still open: the plain fields of `Invoice` (`DueDate`, `Notes`, `Year`/`Month`,
> `RateId`, `Hours`…) keep public setters, and `Customer`/`Tax` were not changed.

`Core/Entities/Invoice.cs` defines transition methods, but:

- **`MarkAsPaid()`** only rejects `Cancelled`. A `Draft` (no number taken yet, see `InvoiceNumbering.PeekAsync`) can go straight to `Paid`, and `Paid → Paid` is allowed.
- **`Cancel()`** only rejects `Paid`, so `Draft → Cancelled` and `Cancelled → Cancelled` both work. Whether cancelling a numbered invoice should require a rectifying invoice (Verifactu/Spanish rules) is not modelled.
- **All 31 properties have public setters**, including `Status`, `Number`, `Subtotal`, `Total`. External code writes them directly:
  - `ImportExportController.cs:470-473` sets `invoice.Total` and `invoice.Status`.
  - `FinalizeInvoiceCommandHandler.cs:56`, `BulkChangeInvoiceStatusCommandHandler.cs:86` and `DraftInvoiceSynchronizer.cs:68` assign `invoice.Number`.
  - `Total` can thus diverge from `Subtotal + TotalExpenses + TotalTaxes`, which `RecalculateTotal()` is meant to guarantee.
- Entities stamp `UpdatedAt = DateTime.UtcNow` themselves, although `DbContext.SaveChangesAsync` already does this.

**Recommendation**
- Make the transitions an explicit table (`Draft→Finalized→Sent→Paid`, `*→Cancelled` with rules) and test it exhaustively with `[TestCase]` over every `(from, to)` pair.
- Make setters `private`/`init`. Expose intent-revealing methods: `AssignNumber(InvoiceNumber)` (only while draft or during finalization), `Finalize(InvoiceNumber)`, and a dedicated `static Invoice Restore(...)` factory for import that validates consistency.
- Remove the `UpdatedAt` stamping from entities and let the DbContext own it.
- Apply the same treatment to `Customer` (12 public setters) and `Tax` (11).

## 4. No global error handling; exceptions as an untyped protocol — High — ✅ Done (2026-10-05)

> **Resolution.** Two exception types carry the meaning: `Core/Exceptions/DomainException` (a business rule
> refused; optional `Code` and `Issues`; derives from `InvalidOperationException`) and
> `Application/Exceptions/NotFoundException` (derives from `KeyNotFoundException`). 31 not-found throws and 39
> business-rule throws were converted; `ComplianceValidationException`, `VerifactuException` and
> `InvoiceEmailException` are now `DomainException`s. One MVC filter, `Api/Infrastructure/ApiExceptionFilter`
> (it replaces point 1's `ValidationExceptionFilter`), maps them to ProblemDetails: 404 / 400 / 409 (Gmail
> setup) / 400 for entity `ArgumentException`s and validation, and 500 with a generic message for anything
> else (the exception only in Development; nothing internal leaks). `AddProblemDetails` adds `error` to every
> ProblemDetails, so the web app's `errorMessage()` works for all responses. 40 translating `try/catch` blocks
> were removed from 11 controllers (−370 lines); the deliberate ones remain (import per item, best-effort Gmail
> revoke, concurrent access request). Fixed along the way: "invoice not found" was a 400, and Holidays,
> InvoiceTemplates, Projects, Rates and Taxes answered `{ message }`, which the web app didn't read (users saw
> "Request failed with status code 400"). An MVC filter rather than `IExceptionHandler` was chosen: all
> endpoints are controllers, and middleware-level errors keep the framework's behaviour.

- There is no `IExceptionHandler`, `UseExceptionHandler` or `AddProblemDetails`. Every controller action has its own `try/catch` that converts exceptions to `BadRequest(new { error = ... })`. `InvoicesController` alone has 15 such blocks.
- Meaning depends on built-in exception types. `InvalidOperationException` means "not found" in one place (`FinalizeInvoiceCommandHandler`: _"Invoice with ID … not found"_ → **400**, should be 404), "illegal state" in another, and "template missing" in a third. `KeyNotFoundException` means 404 in some handlers and not others.
- `catch (Exception)` appears 23 times in `src`. Some swallow unexpected failures into a 500 with a generic message, which duplicates what the framework already does and loses the ProblemDetails `traceId`.
- Error payloads are inconsistent: `{ error }`, `{ error, code }`, `{ error, submission }`, `{ message }`. The frontend compensates with `response?.data?.error` lookups in 10 places.

**Recommendation**
- Introduce a small exception hierarchy in Application: `NotFoundException`, `ConflictException`/`DomainRuleViolationException`, `ValidationException` (FluentValidation's). Or adopt a `Result<T>` type for expected failures.
- Add one `IExceptionHandler` that maps them to RFC 7807 `ProblemDetails` (404/409/400/422). Register `AddProblemDetails()`.
- Remove the per-action `try/catch` blocks. Controllers shrink to one line per action.
- Have the frontend `client.ts` interceptor normalise ProblemDetails into one typed `ApiError`.

## 5. Controllers bypass the Application layer — Medium — ✅ Done (2026-10-06)

> **Resolution.** No controller reaches data any more; every one of them only takes `IMediator` (plus
> data-free Application services for template/email previews). `ArchitectureTests` fails the build when a
> controller takes a repository, `IUnitOfWork` or an Infrastructure type, or when a layer references an outer
> one. Per controller: `InvoicesController` lost the service locator and the PDF/report orchestration
> (`InvoiceDocumentQueries`: invoice/calendar/monthly-report PDFs as `DownloadFile`, regenerate, verify; the
> double load of `ExportPdf` is gone); the template controllers' list/get/delete/activate became queries and
> commands; image assets moved to `Features/ImageAssets` (size/type rules in a validator, alias uniqueness a
> `DomainException`; the controller only reads the upload); access requests and account deletion became
> commands over two small ports (`IAccessRequestRepository.TryAddAsync` owns the concurrent-insert race,
> `IAccountDataEraser`); the Keycloak console link is derived once at startup. `ImportExportController` (770
> lines) is now 90: its EF-heavy bulk transfer moved verbatim behind `IDataPortability` into
> `Infrastructure/DataPortability`, with its "nothing to import" checks as validators. Along the way: every
> automatic model-binding 400 now carries the field messages in `error` (it had only the generic title), and
> `CS0108` (`AccessRequestsController.Request`) is gone.

The project documents controllers as _"thin controllers that dispatch through MediatR"_. That holds for some controllers only:

- **Service locator**: `InvoicesController` calls `HttpContext.RequestServices.GetRequiredService<…>()` 10 times (repositories, `IMonthlyReportGenerationService`, `IHtmlToPdfConverter`, `IInvoiceGenerationService`). The dependencies are hidden and the actions are hard to test.
- **Business logic in the API**: `ExportPdf`, `ExportCalendarPdf`, `ExportMonthlyReportPdf`, `RegenerateInvoicePdf` and `RegenerateMonthlyReportPdf` load entities, check invoice type, resolve customers and orchestrate rendering, with no query/command behind them. `ExportPdf` loads the same invoice **twice** (once via MediatR, once via the repository).
- **Infrastructure leaking upward**: 7 controllers inject `IRepository<T>`, `IUnitOfWork` or the concrete `ApplicationDbContext`. `ImportExportController` (768 lines) is a full import/export service written against `ApplicationDbContext`. It holds the mapping, the rate-by-position logic and the direct entity mutation from §3, none of which can be reused or unit-tested without the web host.

**Recommendation**
- Move PDF/report endpoints behind queries (`GetInvoicePdfQuery`, `GetMonthlyReportPdfQuery`…) returning a `FileResult`-agnostic `DocumentDto(byte[] Content, string ContentType, string FileName)`. `GetInvoiceDocumentsZipQuery` already follows this pattern.
- Extract `ImportExportController` into `Application/Features/ImportExport` (an exporter and an importer service plus commands). Keep the controller to HTTP concerns only.
- Add an architecture test (NetArchTest or ArchUnitNET) asserting `nInvoices.Api.Controllers` does not depend on `nInvoices.Infrastructure` or on `IRepository<>`.

## 6. Two schema-evolution mechanisms — Medium — ✅ Done (2026-10-06)

> **Progress (2026-10-06).** `PostgreSqlMigrationScriptsTests` (Testcontainers, PostgreSQL 17 as in production)
> runs every script with `psql` like `deploy.sh`: on a model-built schema they must change nothing and be
> idempotent, and production's schema (`pg_dump --schema-only` fixture) plus the scripts must equal the model.
> That found: `deploy.sh --migrate` failed on every fresh install (`__EFMigrationsHistory` missing, fixed by
> `00000000-0000_ef-migrations-history.sql`), and production had drifted from the model — a **global** unique
> index on image aliases (a second user couldn't have a "logo"), no unique "one active template" index,
> `InvoiceNumber varchar(50)`, `int`/serial ids, an obsolete empty `InvoiceLines` table, other names and
> defaults. The model now has a per-user alias index, and `20261006-0753_align-with-ef-model.sql` brings
> production to exactly the model (tested, including data: rows kept, ids continue). **Applied to production
> on 2026-10-06** after a full backup (`~/backups/ninvoices/ninvoices-backup-2026-10-06-08-04-44.sql.gz`); a fresh
> schema dump of production now passes the model-equality test (it is the committed fixture; the pre-alignment
> dump is kept to test the script).
>
> **EF migrations for PostgreSQL.** `src/nInvoices.Infrastructure.Migrations.PostgreSql` holds the PostgreSQL
> migration set (one assembly per provider, as EF documents); its baseline `20261006081011_PostgreSqlBaseline`
> builds exactly the model (tested) and is recorded as applied in production. The API applies pending migrations
> at startup on PostgreSQL (fresh installs get the whole schema; `EnsureCreated` is SQLite-only now), so deploys
> need no SQL step. `MigrationsUpToDateTests` fails when the model changes without a migration for each
> provider. The hand-written scripts are frozen (only to bring a database restored from an older backup up to
> the baseline).

`Program.cs` documents it: EF migrations are SQLite-scaffolded and do not run on PostgreSQL. Production schema changes are applied by hand-written scripts in `docker/migrations-postgres/*.sql` (16 so far). Meanwhile `Database:EnsureCreated` builds fresh PG databases from the **current model**. So the same schema is defined three ways (EF model, EF SQLite migrations, PG SQL scripts) and nothing checks that they agree. The test suite runs on SQLite only, so PG-specific behaviour (case sensitivity, `text` vs `numeric`, index names) is never exercised.

**Recommendation**
- Keep provider-specific migration sets (`Migrations/Sqlite`, `Migrations/Postgres`, one migrations assembly or folder per provider; this is the pattern EF documents). Let `dotnet ef database update` / `MigrateAsync` own production, and retire the hand-written scripts.
- At minimum, add a CI test with Testcontainers PostgreSQL that applies all `.sql` scripts to an empty DB and compares the result with `EnsureCreated` output, or runs the integration tests on it.
- Decide whether SQLite is still a supported production target. If it's only for development, consider using PG everywhere (Aspire already provides it) and drop one provider.

## 7. Sync-over-async in the PDF path — Medium

`PdfExportService.GenerateInvoicePdf` calls `_htmlToPdfConverter.ConvertAsync(...).GetAwaiter().GetResult()` (`PdfExportService.cs:30`). This contradicts the project's own rule ("never `.Result` / `.Wait()`"). It blocks a thread-pool thread for the full headless-Chrome render on every PDF download and every email-draft attachment. `IPdfExportService` (sync, in **Core**) also takes no `CancellationToken`.

**Recommendation**: make `IPdfExportService` async (`Task<byte[]> GenerateInvoicePdfAsync(Invoice, CancellationToken)`). Move it from Core to Application next to `IHtmlToPdfConverter`: PDF output is an application concern, not domain.

## 8. Dead and duplicated code — Medium

| Item | Evidence | Action |
|------|----------|--------|
| `QuestPdfHtmlConverter` (548 lines, HTML → QuestPDF via HtmlAgilityPack) | Not referenced anywhere; `AddPdfExport` registers `PuppeteerPdfConverter` | Delete; drop HtmlAgilityPack if unused elsewhere |
| `ITemplateEngine` / `HandlebarsTemplateEngine` + tests | Superseded by Scriban (§2) | ✅ Deleted |
| 18 FluentValidation validators | Never run (§1) | ✅ Wired up |
| `src/nInvoices.Web/src/counter.ts`, `typescript.svg` | Vite scaffold leftovers | Delete |
| Finalization logic | Duplicated between `FinalizeInvoiceCommandHandler` and `BulkChangeInvoiceStatusCommandHandler` (fetch customer, `FinalizeInvoice()`, `TakeAsync`, assign number, run lifecycle steps) | Extract one `IInvoiceFinalizer` used by both, so a new step or rule cannot be added to one path only |
| Status-transition endpoints | `Finalize`/`MarkAsSent`/`MarkAsPaid`/`Cancel` are four copies of the same 15 lines | Collapses once §4 is done |
| DTO mapping | `Mappings/` has 8 mappers, yet 11 handlers keep a private `MapToDto` (e.g. `InvoiceTemplateDto` built in 3 places) | Move all mapping into `Mappings/` |

## 9. Oversized classes and components — Medium

Size is a symptom, not the problem. These files mix several responsibilities, though:

| File | Lines | Concern |
|------|------:|--------|
| `Application/Services/InvoiceGenerationService.cs` | 937 | 12 constructor dependencies. Combines input validation, rate resolution, subtotal/expense math, work-day persistence, project allocation merging, template model building, rendering and PDF. The `persist: bool` flag switches between preview and save behaviour inside one method. |
| `Api/Controllers/ImportExportController.cs` | 768 | See §5 |
| `Api/Controllers/InvoicesController.cs` | 742 | See §4/§5 |
| `Web/src/views/invoices/InvoicesList.vue` | 944 | List, filters, bulk actions, dialogs |
| `Web/src/views/templates/TemplateEditor.vue` | 811 | Editor, preview, variables panel |
| `Web/src/views/settings/Settings.vue` | 771 | Many panels in one view |
| `Web/src/types/index.ts` | 725 | All DTO types in one file, maintained by hand |

**Recommendation**
- Split `InvoiceGenerationService` along its seams: `InvoiceCalculator` (pure: rates + work days + expenses + taxes → amounts; easy to unit-test with no mocks), `WorkDayWriter` (persistence), `InvoiceTemplateModelBuilder`, and a thin orchestrator. Replace `persist: bool` with two explicit call paths sharing the pure core.
- In Vue, move logic into composables (the project already has `useInvoiceDraft`, `useWorkMonth`) and split views into child components.
- Generate `types/index.ts` from the OpenAPI document (`openapi-typescript`, or `Microsoft.Extensions.ApiDescription` plus NSwag). `AddOpenApi()` is already enabled, so frontend and backend contracts stay in sync automatically.

## 10. No CI, no shared build settings, no analyzers — Medium

- No `.github/workflows`: tests and the frontend build are never run automatically, which matters more now that the repo is public.
- No `Directory.Build.props`. `TargetFramework`, `Nullable` and `ImplicitUsings` are repeated in each `.csproj`. Nothing enables `TreatWarningsAsErrors`, `AnalysisLevel`, `EnforceCodeStyleInBuild` or `Deterministic`.
- No `.editorconfig`, so the style rules in `.github/instructions/*.md` (sealed by default, file-scoped namespaces, `var`) are prose that only humans and AI agents enforce.
- No central package management (`Directory.Packages.props`).

**Recommendation**
1. `Directory.Build.props` with `net10.0`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`.
2. `.editorconfig` encoding the documented conventions as diagnostics (e.g. `IDE0161` file-scoped namespaces, `CA1852` seal internal types).
3. `Directory.Packages.props` for versions.
4. A GitHub Actions workflow: `dotnet build` + `dotnet test` (with coverage) + `npm ci && npm run build && npm run lint && npm test`. Add a PG Testcontainers job once §6 exists.

## 11. Frontend quality gates — Medium

- `"build": "tsc && vite build"`: plain `tsc` does **not** type-check `.vue` single-file components, so most of the UI code is never type-checked. Use `vue-tsc --noEmit`.
- No ESLint (`eslint-plugin-vue`, `@typescript-eslint`) or Prettier.
- No unit tests (Vitest + `@vue/test-utils`). Composables like `useInvoiceDraft.ts` (446 lines) and `useWorkMonth.ts` are pure enough to test cheaply.
- 137 occurrences of `any` / loosely typed `catch (e…)` and 28 `alert()`/`confirm()` calls. The project already has `components/ui`, which could host a single confirm/toast service.
- Inconsistent data access: 17 views/components import from `api/` directly, while 8 use Pinia stores. Decide on one rule, e.g. "stores for shared/cached state, direct API calls only for one-off actions", and apply it.
- `v-html` renders user-authored template output (1 site). Make sure it is sandboxed (`<iframe sandbox>`) or sanitised. This is a correctness/security boundary, not just style.

## 12. Inconsistent time source — Low

`TimeProvider` is used in 26 places, but 38 files still call `DateTime.UtcNow` / `DateTime.Today` / `DateTime.Now` directly. Examples: entities stamping `UpdatedAt`, and `InvoicesController.Summary` using `DateTime.Today.Year`, which is the **server's** local date. Tests touching those paths depend on the wall clock, and year-boundary behaviour can't be tested.

**Recommendation**: inject `TimeProvider` everywhere outside the domain and pass dates into entity methods. Add an analyzer ban (`BannedApiAnalyzers` with `BannedSymbols.txt`) on `DateTime.Now/UtcNow/Today`.

## 13. Package hygiene — Low

- `Microsoft.AspNetCore.Http.Abstractions` **2.3.11** (Infrastructure): a legacy ASP.NET Core 2.x package on a net10 project. Use `<FrameworkReference Include="Microsoft.AspNetCore.App" />` instead.
- `Microsoft.Extensions.Options` **9.0.1** (Application), while everything else is 10.0.x.
- `Aspire.Hosting.Keycloak` is a preview build (acceptable since AppHost is dev-only, but pin and track it).
- `QuestPDF.Settings.License` is set inside the `PdfExportService` constructor on every scope. Move it to startup.

## 14. Repository hygiene — Low

- Tracked debugging artefacts at the root: `final-invoice.pdf`, `final-calendar.pdf`, `firefox-debug-*.png`, `test-results.json`, plus a root `package.json`/`package-lock.json` used only by the ad-hoc Playwright scripts.
- Ad-hoc E2E scripts (`e2e-tests.mjs`, `test-pdf-generation.mjs`, `test-monthly-report.mjs`, `tests/test-regenerate-pdfs.mjs`) sit outside any test runner and are not in CI. Move them into a proper `tests/e2e` Playwright Test project, or delete them.
- `docker/` holds 13 Markdown files, several of which are historical incident notes (`JWT-AUTHENTICATION-FIX.md`, `RESOLUTION-SUMMARY.md`, `BUILD-ISSUES.md`, `KEYCLOAK-PERMISSION-FIX.md`). Move them to `Docs/history/` or delete them, so newcomers find the current docs.
- `Docs/Apply_changes_on_db_Docker.txt` is superseded by `docker/migrations-postgres/README.md`.

---

## Suggested order of work

1. **Quick wins (≈1 day)**: §2 (switch template validation to Scriban), §7 (async PDF), §8 dead-code removal, §13 packages, §14 clutter.
2. **Safety net (≈1–2 days)**: §10 CI + `Directory.Build.props` + analyzers; §11 `vue-tsc` + ESLint. Fix the warnings this surfaces.
3. **Correctness (≈2–3 days)**: §1 validation pipeline, §4 exception handling + ProblemDetails, §3 invoice state machine + encapsulation, each with tests.
4. **Structure (ongoing)**: §5 thin controllers + architecture tests, §9 split `InvoiceGenerationService` and large views, OpenAPI-generated TS types, §6 provider-specific migrations with a PG test job.

## What is already good (keep it)

- Correct layer dependency direction and a feature-folder CQRS layout that is easy to navigate.
- Ownership isolation done centrally in `ApplicationDbContext` (global filter, owner stamping, cross-owner FK rejection), with dedicated `OwnershipTests`.
- Envelope encryption with startup re-wrapping, and documented constraints on encrypted columns.
- Value objects for money and invoice numbers; Strategy pattern for tax handlers; extension points (`IInvoiceLifecycleStep`, `IInvoiceTemplateModelContributor`) that keep country-specific compliance out of the core flow.
- Comments that explain _why_, and a test suite of meaningful size on the backend.
