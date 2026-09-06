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

# EF Core migrations — DbContext lives in Infrastructure, startup project is the API
dotnet ef migrations add <Name> -p src/nInvoices.Infrastructure -s src/nInvoices.Api
dotnet ef database update -s src/nInvoices.Api      # or run from src/nInvoices.Api
```

### Frontend (`src/nInvoices.Web`)

```bash
npm install
npm run dev       # Vite dev server on http://localhost:5173
npm run build     # tsc type-check + vite build
npm run preview
```

### Run locally (no Keycloak)

`appsettings.Development.json` sets `Authentication:UseDevAuth = true`, so running the API
directly auto-authenticates every request as a fake `user`+`admin` dev user and uses a local
SQLite file. Two terminals:

```bash
cd src/nInvoices.Api && dotnet run     # API on https://localhost:5001
cd src/nInvoices.Web && npm run dev     # frontend on http://localhost:5173
```

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
- **nInvoices.Application** — CQRS via MediatR, organized by feature under
  `Features/<Entity>/{Commands,Queries,Validators}`. Each command/query is a `sealed record`
  `IRequest<T>` with a sibling `...Handler`. DTOs in `DTOs/`, FluentValidation validators
  alongside features, template rendering (`ScribanTemplateRenderer`) and invoice/report
  generation services in `Services/`, i18n JSON in `Localization/`.
- **nInvoices.Infrastructure** — `ApplicationDbContext`, EF entity configs
  (`Data/Configurations/`), generic `Repository<T>` + `UnitOfWork`, migrations
  (`Data/Migrations/`), tax handler implementations (`TaxHandlers/`), Scriban template engine
  (`TemplateEngine/`), PDF export (`PdfExport/` — QuestPDF and PuppeteerSharp/HtmlAgilityPack),
  `UserContext` (reads JWT claims).
- **nInvoices.Api** — thin controllers (`Controllers/`) that dispatch through MediatR.
  `Program.cs` wires everything via extension methods: `AddDatabase`, `AddTaxHandlers`,
  `AddTemplateEngine`, `AddPdfExport`, `AddApplicationServices`, `AddMediatR`. Auth is either
  `DevAuthenticationHandler` (dev) or Keycloak JWT Bearer. Policies: `RequireUser`,
  `RequireAdmin`.
- **nInvoices.Web** — Vue 3 Composition API. `src/api/` wraps a shared axios `client.ts` with
  one module per resource; `src/stores/` Pinia; `src/services/auth.service.ts` uses
  `oidc-client-ts` for Keycloak. Views in `src/views/`, routing in `src/router/`.

### Persistence conventions

- Repository methods only stage changes; commit explicitly with
  `await _unitOfWork.SaveChangesAsync(ct)` in the handler.
- `DbContext.SaveChangesAsync` auto-stamps `UpdatedAt` on modified `EntityBase` rows.
- Database provider is chosen by `Database:Type` config (`SQLite` | `PostgreSQL`); the
  connection string key is `ConnectionStrings:Default`. Migrations assembly is
  `nInvoices.Infrastructure` for both providers, so a single migration set must work on both.

### Tax calculation (Strategy pattern)

Handlers implement `ITaxHandler` (`HandlerId`, `Description`, `Calculate(...)`). Built-ins:
`PERCENTAGE`, `FIXED`, `COMPOUND`. Add one by implementing the interface in
`Infrastructure/TaxHandlers/` and registering it in `AddTaxHandlers()`; it is then selectable
by `HandlerId`.

### Templates & PDF

User HTML templates use **Scriban** (Liquid-like) syntax with placeholders such as
`{{ customer.name }}`, `{{ invoice.total }}`. Validated by `ValidateTemplateCommand`,
rendered by `ScribanTemplateRenderer`, converted to PDF in `Infrastructure/PdfExport/`.
Sample templates and syntax reference live in `Docs/`.

### Keycloak in Docker

The API container cannot resolve the browser-facing Keycloak URL. `KeycloakBackchannelHandler`
rewrites the external host to the internal `keycloak:8080` for OIDC discovery/JWKS, and
`Program.cs` accepts multiple `ValidIssuers` so tokens minted against either URL validate. See
`docker/KEYCLOAK-DOCKER-GUIDE.md`.

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
3. DTOs in `Application/DTOs/`; commands/queries/validators under
   `Application/Features/<Entity>/`.
4. Controller in `Api/Controllers/` dispatching via MediatR.
5. Frontend: `src/api/<entity>.ts` + store/view as needed.

## Notes

- `.slnx` solution (`nInvoices.slnx`) — needs a recent SDK; `dotnet` commands target it
  automatically from the repo root.
- No `Directory.Build.props`; each `.csproj` sets `net10.0`, `Nullable`, `ImplicitUsings`.
- Numerous `*.md` files at the repo root and in `docker/` are historical build/deploy notes;
  `README.md`, `QUICKSTART.md`, and `docker/KEYCLOAK-DOCKER-GUIDE.md` are the current ones.
