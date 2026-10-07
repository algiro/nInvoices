# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

nInvoices is a freelancer invoice management system: a .NET 10 Clean Architecture backend
(`src/nInvoices.*`) plus a Vue 3 + TypeScript frontend (`src/nInvoices.Web`). It manages
customers, rates, taxes, worked days, and generates PDF invoices from user-defined HTML
templates.

## Commands

### Backend

```bash
dotnet build                                   # build the solution (nInvoices.slnx)
dotnet test                                    # run all test projects
dotnet test tests/nInvoices.Application.Tests  # run one test project
dotnet test --filter "FullyQualifiedName~CreateCustomer"   # run a single test / class
dotnet test --filter "DisplayName~Scenario"
dotnet test /p:CollectCoverage=true /p:CoverageReportsFormat=opencover

# EF Core migrations: one set per provider. A model change needs BOTH (MigrationsUpToDateTests fails otherwise)
dotnet ef migrations add <Name> -p src/nInvoices.Infrastructure -s src/nInvoices.Api --context ApplicationDbContext   # SQLite
dotnet ef migrations add <Name> -p src/nInvoices.Infrastructure.Migrations.PostgreSql -s src/nInvoices.Infrastructure.Migrations.PostgreSql --context ApplicationDbContext   # PostgreSQL
dotnet ef database update -s src/nInvoices.Api --context ApplicationDbContext      # local SQLite database
```

**Build settings are shared**: `Directory.Build.props` (net10.0, nullable, analyzers at
`latest-recommended`, `EnforceCodeStyleInBuild`, **`TreatWarningsAsErrors`**) and `Directory.Packages.props`
(central package versions: a `.csproj` has `<PackageReference Include="X" />` without `Version`; add or bump
versions there). Any warning fails the build. Fix it; if a rule truly doesn't apply, turn it off in
`.editorconfig` with the reason, or `#pragma warning disable` at the site with a comment (as for SHA-1 in
FACe). `.editorconfig` also encodes the C# conventions (file-scoped namespaces, sealed types). Stop a running
API before building, or its DLLs are locked. `docker/Dockerfile.api` copies the root `Directory.*.props`
before restoring; keep that in step.

**CI** (`.github/workflows/ci.yml`, every push to main and every PR): backend Release build and tests
(PostgreSQL tests run on the runner's Docker), frontend `npm run check` + build, and both Docker images build.

### Frontend (`src/nInvoices.Web`)

```bash
npm install
npm run dev       # Vite dev server on http://localhost:5173
npm run build     # vue-tsc type-check (incl. .vue files) + vite build
npm run preview
npm run check     # what to run before committing: typecheck + lint + test
npm run typecheck # vue-tsc --noEmit (plain tsc does not see inside .vue files)
npm run lint      # ESLint (eslint.config.js: typescript-eslint + eslint-plugin-vue; layout rules off)
npm run test      # Vitest, src/**/*.test.ts next to the code (Node; `// @vitest-environment jsdom` per file if needed)
```

### Run locally — fast loop (SQLite + DevAuth, no Docker/Keycloak)

`appsettings.Development.json` sets `Authentication:UseDevAuth = true`, so the API
auto-authenticates every request as a fake `user`+`admin` dev user and uses a local SQLite
file. `src/nInvoices.Web/.env.development` sets `VITE_AUTH_DISABLED=true`. Two terminals:

```bash
cd src/nInvoices.Api && dotnet run     # API on http://localhost:5297 (Properties/launchSettings.json)
cd src/nInvoices.Web && npm run dev     # frontend on http://localhost:3000, /api proxied to :5297
```

`Properties/launchSettings.json` is gitignored — without it `dotnet run` starts in **Production**
and the Keycloak auth path fails (`MetadataAddress ... must use HTTPS`). Restore it with an `http`
profile that sets `ASPNETCORE_ENVIRONMENT=Development` and `applicationUrl=http://localhost:5297`.
Schema: `dotnet ef database update` (SQLite only — see below).

### Run locally — prod parity (PostgreSQL + Keycloak) via Aspire

`src/nInvoices.AppHost` is a **dev-only** .NET Aspire orchestrator. Needs Docker running.

```bash
cd src/nInvoices.Web && npm install     # once
aspire run --project src/nInvoices.AppHost      # or: dotnet run --project src/nInvoices.AppHost
```

Brings up PostgreSQL + Keycloak (realm imported from `src/nInvoices.AppHost/keycloak/`, port
8088, test user `testuser` / `Test123!`) + the API (real Keycloak auth; it applies the PostgreSQL EF
migrations at startup) + the Vite dev server, with the Aspire dashboard. No data
volumes — every run starts clean. Never deployed.

### Run full stack (Docker + Keycloak + PostgreSQL)

```bash
cd docker
cp ../.env.example .env      # use alphanumeric-only passwords — special chars break services
docker-compose -f docker-compose.dev.yml up -d   # web :3000, api :8080, keycloak :8080
```

### E2E / smoke scripts (repo root, Playwright)

`e2e-tests.mjs`, `test-pdf-generation.mjs`, `test-monthly-report.mjs` — run with `node <file>`
against a running API + frontend.

## Architecture

### Layer dependency direction

`Core` ← `Application` ← `Infrastructure` ← `Api`. `Core` has no project dependencies.
`Web` talks to `Api` only over HTTP.

- **nInvoices.Core** — entities (`Entities/`), value objects (`ValueObjects/`, e.g. `Address`,
  `Money`, `InvoiceNumber`), enums, and interfaces (`IRepository<T>`, `IUnitOfWork`,
  `IUserContext`, `ITaxHandler`). All entities derive from `EntityBase` (`long Id`,
  `CreatedAt`, `UpdatedAt`).
- **nInvoices.Application** — CQRS via [Mediator](https://github.com/martinothamar/Mediator)
  (MIT, source-generated; replaced MediatR when it went commercial), organized by feature under
  `Features/<Entity>/{Commands,Queries,Validators}`. Each command/query is a `sealed record`
  `IRequest<T>` with a sibling `...Handler` whose `Handle` returns `ValueTask<T>`. Handlers are
  wired at compile time: a request without a handler is a build error. Registered **Scoped**
  (the generator's default is Singleton, which would capture the DbContext); in tests, wrap a
  handler call passed to `Should.ThrowAsync` in `.AsTask()`. DTOs in `DTOs/`, FluentValidation validators
  alongside features, template rendering (`ScribanTemplateRenderer`) and invoice/report
  generation services in `Services/`, i18n JSON in `Localization/`. Invoice generation
  (`InvoiceGenerationService`) only orchestrates; its parts live in `Services/InvoiceGeneration/`:
  `InvoiceCalculator` (pure: billable checks, subtotal, expenses, day hours), `InvoiceTemplateModelBuilder`
  (pure: line items, project summary), `InvoiceRateResolver` (default + per-day rates → `DayRates`) and
  `InvoiceWorkDays` (load / replace a month's work days). Put arithmetic in the calculator and test it
  with plain values (`InvoiceCalculatorTests`).
  **Validation**: `AddApplicationRequests()` registers Mediator with `Behaviors/ValidationBehavior`,
  which runs every `IValidator<TRequest>` before the handler and throws `ValidationException`;
  the API's `ApiExceptionFilter` returns it as a 400 `ValidationProblemDetails` (`errors`
  per field plus an `error` string). Rules live in DTO validators; a command carrying a DTO gets
  a command validator delegating to it (`Features/*/Validators/*CommandValidators.cs`).
  `CommandValidatorsTests` fails if a request carries a validated DTO without one.
- **nInvoices.Infrastructure** — `ApplicationDbContext`, EF entity configs
  (`Data/Configurations/`), generic `Repository<T>` + `UnitOfWork`, migrations
  (`Data/Migrations/`), tax handler implementations (`TaxHandlers/`), PDF export (`PdfExport/`: `PdfExportService`
  implements Application's `IPdfExportService` — rendered templates through headless Chrome (PuppeteerSharp,
  async), built-in layouts through QuestPDF),
  `UserContext` (reads JWT claims), and the implementations of Application/Core ports that need EF Core
  directly: `DataPortability/DataPortabilityService` (`IDataPortability`: JSON import/export, delegating to
  one class per kind of data: `CustomerPortability`, `SharedTemplatesPortability`, `InvoicePortability`,
  `SettingsPortability`; `ImportExportRoundTripTests` backs up and restores through all of them),
  `AccessRequestRepository` (concurrent-insert race), `AccountDataEraser` (account deletion).
- **nInvoices.Api** — thin controllers (`Controllers/`) that only translate HTTP: they dispatch through Mediator
  (`IMediator.Send`) and never take a repository, `IUnitOfWork`, the DbContext or any Infrastructure type, nor
  resolve services from `HttpContext.RequestServices` (`ArchitectureTests` enforces the constructor rule and
  the layer direction). Application services that don't touch data (template preview/validation, email
  preview) may be injected. HTTP-only work stays in the controller: reading an uploaded file, claims,
  turning a `DownloadFile` into a file response.
  `Program.cs` wires everything via extension methods: `AddServiceDefaults`, `AddDatabase`,
  `AddTaxHandlers`, `AddPdfExport`, `AddApplicationServices`, `AddApplicationRequests` (Mediator + validators).
  Auth is either `DevAuthenticationHandler` (dev) or Keycloak JWT Bearer. Policies:
  `RequireUser`, `RequireAdmin`.
- **nInvoices.ServiceDefaults** — Aspire shared project (`AddServiceDefaults()` /
  `MapDefaultEndpoints()`): OpenTelemetry traces + metrics, HTTP resilience, service discovery,
  and the `/health` (all checks incl. `AddDbContextCheck`) + `/alive` (liveness) endpoints.
  Referenced only by `Api`. OTLP export activates when `OTEL_EXPORTER_OTLP_ENDPOINT` is set;
  Serilog still owns log sinks (`HealthController` at `/api/health` is kept for the nginx-routed
  check).
- **nInvoices.AppHost** — dev-only Aspire orchestrator (`aspire run`): PostgreSQL + Keycloak +
  API + Vite. Not referenced by any deployable project, never published. Production deploy is
  unchanged (Docker Hub + compose + SSH); Aspire is not used for deployment. Full walkthrough
  (images, wiring, config precedence, ports, troubleshooting): `src/nInvoices.AppHost/README.md`.
- **nInvoices.Web** — Vue 3 Composition API. `src/api/` wraps a shared axios `client.ts` with
  one module per resource; `src/stores/` Pinia; `src/services/auth.service.ts` uses
  `oidc-client-ts` for Keycloak. Views in `src/views/`, routing in `src/router/`. A view keeps the page's
  flow; its parts go in `src/components/<area>/` (e.g. `invoices/list/`, `templates/editor/`, `settings/*Panel`)
  and its logic in composables (`useInvoiceListQuery` holds the invoice list's URL state, `templateKinds.ts`
  hides the three template APIs behind one interface). Scoped CSS moves with the markup it styles.
  **Data access**: a Pinia store holds state that several views share or cache (customers, projects,
  rates, taxes, settings, auth); a one-off read or action of a single view or panel calls `src/api/`
  directly. **Errors**: `catch (error)` stays `unknown`; read it through `errorMessage(error, fallback)`,
  `apiStatus`, `apiErrorCode`, `apiErrorData` (`composables/useToast.ts`) or `toast.failure(title, error)`,
  never `error: any`. No `alert()`/`confirm()`: use `useToast` / `useConfirm`. `v-html` only for
  server-generated markup (lint warns); user-authored HTML goes in a sandboxed `<iframe srcdoc>` without
  `allow-scripts`.

### Persistence conventions

- Repository methods only stage changes; commit explicitly with
  `await _unitOfWork.SaveChangesAsync(ct)` in the handler.
- `DbContext.SaveChangesAsync` auto-stamps `UpdatedAt` on modified `EntityBase` rows.
- **Data is per user.** User-data entities derive from `OwnedEntityBase` (`IOwnedEntity.OwnerId`
  = the JWT `sub`). `ApplicationDbContext` adds a global query filter `OwnerId == current user`
  (no user → no rows), stamps `OwnerId` on insert, and rejects saves that modify another user's
  row or set a foreign key to a row the user can't see. Handlers need no owner code; don't use
  `IgnoreQueryFilters()` in request paths. Synchronous `SaveChanges` throws. New user-data
  entities should derive from `OwnedEntityBase`; per-user unique indexes include `OwnerId`.
  Rows with an empty owner (pre-multi-user data) are assigned at startup to
  `MultiUser:LegacyOwnerId` (`UnownedDataExtensions`).
- **Sensitive columns are encrypted** (`.IsEncrypted("Entity.Column")` in the EF configuration,
  per-user keys wrapped by a master key file; `Docs/ENCRYPTION.md`). They are stored as text even
  when decimal, so **never filter, sort, group or sum them in SQL**: load and do it in memory
  (see `InvoiceRepository`). Comparing one in a query throws. Never rename a purpose string, and
  don't touch encrypted columns with `ExecuteUpdate` or raw SQL. `ApplicationDbContext` takes a
  `FieldEncryptor`; tests use `TestEncryption.Encryptor`.
- **Schema changes are EF migrations, one per provider**: SQLite in `Infrastructure/Data/Migrations`, PostgreSQL in
  `nInvoices.Infrastructure.Migrations.PostgreSql` (commands above). On PostgreSQL the API applies pending
  migrations at startup (`Program.cs`), so a deploy needs nothing else. `MigrationsUpToDateTests` fails when the
  model changed without a migration for each provider. `dotnet test --filter Category=PostgreSql` (needs Docker)
  checks on a real PostgreSQL 17 that the migrations build exactly the model and that production's schema
  (`tests/.../PostgreSql/production-schema.sql`, a pg_dump) has nothing pending. `docker/migrations-postgres` is
  frozen (only for databases restored from backups older than 2026-10-06).
- Database provider is chosen by `Database:Type` config (`SQLite` | `PostgreSQL`); the
  connection string key is `ConnectionStrings:Default`. Each provider has its own migrations assembly:
  `nInvoices.Infrastructure` for SQLite, `nInvoices.Infrastructure.Migrations.PostgreSql` for PostgreSQL
  (`DatabaseExtensions.PostgreSqlMigrationsAssembly`).

### Invoice lifecycle

Draft → Finalized → (Sent) → Paid, and Finalized/Sent → Cancelled; Paid and Cancelled are final. Only a
draft can be deleted (it was never issued); an issued invoice is cancelled instead and keeps its number
on record (`DELETE ?force=true` bypasses this, on explicit request). The table lives in one place,
`Core/Entities/InvoiceLifecycle.cs` (`WhyNot(status, action)`, actions in `InvoiceAction`, `Delete`
being the one that changes no status); `Invoice` enforces it (`Finalize(number)`, `MarkAsSent`,
`MarkAsPaid`, `Cancel`, `EnsureAllowed` throw `InvalidOperationException` with the reason), the delete
handler checks it, and the bulk actions report its reasons.
`Status`, `Number` and the money totals have private setters: change them only through those methods,
`RenumberDraft`, `AddExpenses`/`AddTaxes`, or `RestoreImported` (import only). `InvoiceLifecycleTests`
covers every status × action pair. The frontend menu (`useInvoiceActions.actionsFor`) mirrors the table.
Finalizing (one invoice or in bulk) goes through `IInvoiceFinalizer`: `FinalizeAsync` before the save
(takes the number, runs the `IInvoiceLifecycleStep`s), `CompleteAsync` after it (re-renders, moves the
other drafts on, publishes `InvoiceFinalizedNotification`). Change finalization there, never in a handler.

### Tax calculation (Strategy pattern)

Handlers implement `ITaxHandler` (`HandlerId`, `Description`, `Calculate(...)`). Built-ins:
`PERCENTAGE`, `FIXED`, `COMPOUND`. Add one by implementing the interface in
`Infrastructure/TaxHandlers/` and registering it in `AddTaxHandlers()`; it is then selectable
by `HandlerId`.

### Templates & PDF

User HTML templates use **Scriban** (Liquid-like) syntax written with `[[ ]]` delimiters (turned into
Scriban's `{{ }}` before parsing, so they don't clash with Vue), e.g. `[[ customer.name ]]`,
`[[ for line in lineItems ]]…[[ end ]]`. Scriban is the only template engine: `ScribanTemplateRenderer`
both renders and validates (`ValidateAsync`). Saving any template (invoice, monthly report, email subject
and body) goes through a Mediator command whose validator applies `Validation/TemplateSyntaxRules.MustBeValidTemplate`,
so a template that doesn't parse is rejected (400) with its line/column; the editors' validate endpoints use the same check, and the HTML is converted to PDF in `Infrastructure/PdfExport/`.
Sample templates and syntax reference live in `Docs/`.

### Keycloak in Docker

The API container cannot resolve the browser-facing Keycloak URL. `KeycloakBackchannelHandler`
rewrites the external host to the internal `keycloak:8080` for OIDC discovery/JWKS, and
`Program.cs` accepts multiple `ValidIssuers` so tokens minted against either URL validate. See
`docker/KEYCLOAK-DOCKER-GUIDE.md`.

## Errors

Throw, don't catch-and-translate in controllers: `Api/Infrastructure/ApiExceptionFilter` turns every
exception into a ProblemDetails response that also carries `error` (the message the web app shows), plus
`code` / `issues` when the exception has them.

| Throw | When | Response |
|---|---|---|
| `NotFoundException` (Application) | the resource of the request, or its customer, doesn't exist | 404 |
| `DomainException` (Core; optional `Code`, `Issues`) | a business rule refuses (message is for the user) | 400 |
| `InvoiceEmailException` (a `DomainException`) | email can't be drafted; Gmail setup codes | 409, else 400 |
| `ArgumentException` from an entity | invalid input to a constructor/method | 400 |
| FluentValidation `ValidationException` | the validation pipeline | 400 + `errors` per field |
| anything else | a bug | 500, logged, generic message |

`DomainException` derives from `InvalidOperationException` and `NotFoundException` from
`KeyNotFoundException`, so internal `catch`es of those still work. Keep `catch` in controllers only for
deliberate local handling (per-item import errors, best-effort cleanup).

## Conventions

Code style and testing rules are authoritative in `.github/instructions/`
(`coding-guidelines.instructions.md`, `coding-style.instructions.md`,
`testing-nunit.instructions.md`, `refactoring-patterns.md`). Key points:

- `sealed` classes by default; `record` for DTOs / immutable data; file-scoped namespaces;
  implicit usings (do not add `using System.*` etc.); `var` for locals.
- Always flow `CancellationToken`; never `.Result` / `.Wait()`; prefer `Try*` methods over
  catching parse/lookup exceptions.
- `ArgumentNullException.ThrowIfNull` only in public methods taking reference types.
- Tests: NUnit + Shouldly (+ Moq when needed), `[TestCase]`/`[TestCaseSource]` for parameters,
  name as `Method_Scenario_ExpectedOutcome`, use `TestContext.Current.CancellationToken`.

## Adding a feature (entity end-to-end)

1. Entity in `Core/Entities/` (derive `EntityBase`); EF config in
   `Infrastructure/Data/Configurations/`.
2. Add `DbSet` to `ApplicationDbContext`, then create a migration (command above).
3. DTOs in `Application/DTOs/`, entity → DTO mapping in one `Application/Mappings/<Entity>Mapper`
   (handlers call it, never a private `MapToDto` copy); commands/queries/validators under
   `Application/Features/<Entity>/` (a DTO validator plus a command validator wrapping it).
4. Controller in `Api/Controllers/` dispatching via Mediator (no repositories: `ArchitectureTests` fails otherwise).
5. Frontend: `src/api/<entity>.ts` + store (only if shared, see Data access) / view as needed;
   `npm run check` must pass.

## Notes

- `.slnx` solution (`nInvoices.slnx`) — needs a recent SDK; `dotnet` commands target it
  automatically from the repo root.
- No `Directory.Build.props`; each `.csproj` sets `net10.0`, `Nullable`, `ImplicitUsings`.
- Numerous `*.md` files at the repo root and in `docker/` are historical build/deploy notes;
  `README.md`, `QUICKSTART.md`, and `docker/KEYCLOAK-DOCKER-GUIDE.md` are the current ones.
