# nInvoices - Copilot Instructions

## Overview

nInvoices is a freelancer invoice management system built with .NET 10 and Vue 3, following Clean Architecture principles with CQRS pattern.

## Build & Test Commands

### Backend (.NET)

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/nInvoices.Core.Tests
dotnet test tests/nInvoices.Application.Tests
dotnet test tests/nInvoices.Infrastructure.Tests
dotnet test tests/nInvoices.Api.Tests

# Run single test (use --filter)
dotnet test --filter "FullyQualifiedName~NameOfYourTest"
dotnet test --filter "DisplayName~PartOfTestName"

# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportsFormat=opencover

# Apply database migrations
cd src/nInvoices.Api
dotnet ef database update
```

### Frontend (Vue 3)

```bash
cd src/nInvoices.Web

# Install dependencies
npm install

# Run development server
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview
```

### E2E Tests (Playwright)

```bash
# Run from repository root
node e2e-tests.mjs
```

### Run Development Environment

**Option 1: Manual (Two Terminals)**
```bash
# Terminal 1 - API
cd src/nInvoices.Api
dotnet run
# API runs at https://localhost:5001

# Terminal 2 - Frontend
cd src/nInvoices.Web
npm run dev
# Frontend runs at http://localhost:5173
```

**Option 2: Docker with Keycloak (Full Stack)**
```bash
cd docker
cp ../.env.example .env
# Edit .env with your settings
docker-compose -f docker-compose.dev.yml up -d
# Access at http://localhost:3000
```

## Architecture

### Clean Architecture Layers

1. **nInvoices.Core** - Domain Layer
   - Entities: Customer, Invoice, Rate, Tax, WorkDay, Expense
   - Value Objects: InvoiceNumber, Money, etc.
   - Interfaces: ITaxHandler, IRepository<T>, IUnitOfWork
   - No dependencies on other projects

2. **nInvoices.Application** - Business Logic Layer
   - CQRS Commands/Queries using MediatR
   - DTOs for data transfer
   - FluentValidation validators
   - Template validation and rendering logic
   - Depends on: nInvoices.Core

3. **nInvoices.Infrastructure** - Data Access & External Services
   - Entity Framework Core with ApplicationDbContext
   - Tax handler implementations (Strategy pattern)
   - Template engine (Scriban/Handlebars)
   - PDF generation (QuestPDF)
   - Database migrations
   - Depends on: nInvoices.Core, nInvoices.Application

4. **nInvoices.Api** - ASP.NET Core Web API
   - Controllers for REST endpoints
   - Keycloak OAuth2/JWT authentication
   - Swagger/OpenAPI documentation
   - Dependency injection configuration
   - Depends on all other projects

5. **nInvoices.Web** - Vue 3 Frontend
   - TypeScript + Composition API
   - Pinia for state management
   - Vue Router for navigation
   - Axios for API calls
   - OIDC authentication with Keycloak

### Key Design Patterns

- **CQRS**: Commands and Queries separated via MediatR (Application layer)
- **Strategy Pattern**: ITaxHandler with multiple implementations (PercentageTaxHandler, FixedAmountTaxHandler, CompoundTaxHandler)
- **Repository Pattern**: Generic IRepository<T> for data access
- **Unit of Work**: IUnitOfWork for transaction management
- **Factory Pattern**: Tax handler creation based on HandlerId

### Database

- **Development**: SQLite (file-based: nInvoices.db)
- **Production**: PostgreSQL (Docker-based)
- Migrations: EF Core migrations in Infrastructure project

### Authentication

- **Keycloak**: OAuth2/OIDC provider
- **JWT Bearer Tokens**: API authentication
- **BackchannelHandler**: Required for Docker containerization - rewrites localhost URLs to container names for JWT validation

## Key Conventions

### Code Style

- Follow `.github/instructions/coding-guidelines.instructions.md` and `coding-style.instructions.md`
- Prefer records for DTOs and immutable data
- Make classes `sealed` by default
- Use `var` for local variables
- Use range indexers over LINQ when clearer
- Mark nullable fields explicitly with `?`
- Use `ArgumentNullException.ThrowIfNull()` only for public methods with reference types

### Testing

- Follow `.github/instructions/testing-nunit.instructions.md`
- Test framework: NUnit
- Assertion library: Shouldly
- Mocking: Moq (when needed)
- Use `[TestCase]` or `[TestCaseSource]` for parameterized tests
- Use `TestContext.Current.CancellationToken` for cancellation
- Name tests: `MethodName_Scenario_ExpectedOutcome`

### CQRS Pattern

Commands and queries are handled by MediatR:

```csharp
// Command example
public sealed record CreateCustomerCommand(string Name, string Email) : IRequest<CustomerDto>;

public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, CustomerDto>
{
    public async Task<CustomerDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        // Implementation
    }
}

// Query example
public sealed record GetCustomerByIdQuery(int Id) : IRequest<CustomerDto?>;

public sealed class GetCustomerByIdQueryHandler : IRequestHandler<GetCustomerByIdQuery, CustomerDto?>
{
    public async Task<CustomerDto?> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

### Tax Calculation Strategy

Tax handlers implement ITaxHandler:

```csharp
public interface ITaxHandler
{
    string HandlerId { get; }
    string Description { get; }
    decimal Calculate(decimal baseAmount, decimal rate, IDictionary<string, decimal>? context = null);
}
```

Built-in handlers:
- **PERCENTAGE**: Simple percentage-based tax
- **FIXED**: Fixed amount regardless of base
- **COMPOUND**: Multiple tax rates applied sequentially

### Template System

- Uses Scriban template engine (Liquid-like syntax)
- Templates stored as HTML with placeholders: `{{ customer.name }}`, `{{ invoice.total }}`
- Validation via ValidateTemplateCommand
- PDF generation via QuestPDF
- Sample templates in `Docs/` folder

### Invoice Numbering

Configurable format via InvoiceSettings:
- Pattern: `INV-{YEAR}-{NUMBER:000}`
- Supports {YEAR}, {MONTH}, {DAY}, {NUMBER} placeholders
- Auto-increments per format pattern

## Database Context

Primary DbContext: `ApplicationDbContext` in Infrastructure project

```csharp
public sealed class ApplicationDbContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<Rate> Rates { get; set; }
    public DbSet<Tax> Taxes { get; set; }
    public DbSet<WorkDay> WorkDays { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<InvoiceTemplate> InvoiceTemplates { get; set; }
    public DbSet<MonthlyReportTemplate> MonthlyReportTemplates { get; set; }
}
```

## Docker & Keycloak

**Critical Implementation Detail**: When running in Docker, the API container needs `BackchannelHttpHandler` to validate JWT tokens. This handler rewrites external URLs (e.g., `http://localhost:8080`) to internal Docker network names (e.g., `http://keycloak:8080`) for OIDC discovery.

See `docker/KEYCLOAK-DOCKER-GUIDE.md` for complete implementation details.

**Common Issues**:
- Password special characters in .env can break services - use alphanumeric only
- Keycloak realm configuration must match client IDs in API and frontend
- CORS must allow frontend origin in API configuration

## Documentation

- **README.md**: Project overview and setup
- **QUICKSTART.md**: Docker-based quick start (< 10 minutes)
- **docker/KEYCLOAK-DOCKER-GUIDE.md**: Complete Docker + Keycloak setup guide
- **docker/TROUBLESHOOTING.md**: Common issues and solutions
- **docker/MIGRATION.md**: SQLite to PostgreSQL migration
- **Docs/**: Template syntax reference and implementation guides

## Common Tasks

### Add a New Entity

1. Create entity in `nInvoices.Core/Entities/`
2. Add DbSet to `ApplicationDbContext`
3. Create migration: `dotnet ef migrations add AddEntityName -p src/nInvoices.Infrastructure -s src/nInvoices.Api`
4. Add DTOs in `nInvoices.Application/DTOs/`
5. Create Commands/Queries in `nInvoices.Application/Features/EntityName/`
6. Add controller in `nInvoices.Api/Controllers/`

### Add a New Tax Handler

1. Implement `ITaxHandler` in `nInvoices.Infrastructure/TaxHandlers/`
2. Register in `TaxHandlerExtensions.AddTaxHandlers()`
3. Handler is automatically available via HandlerId

### Modify Templates

Template validation and rendering:
- Validation: `ValidateTemplateCommand` checks syntax and placeholders
- Rendering: `HandlebarsTemplateEngine` renders templates
- PDF: `QuestPdfInvoiceExporter` converts HTML to PDF
