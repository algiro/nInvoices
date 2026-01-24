# nInvoices - Freelancer Invoice Management System

A modern, full-stack invoice management application built with .NET 10 and Vue 3, designed specifically for freelancers to manage customers, rates, taxes, and generate professional invoices.

## 🏗️ Architecture

This application follows **Clean Architecture** principles with clear separation of concerns:

- **nInvoices.Core**: Domain entities, value objects, and interfaces
- **nInvoices.Application**: Business logic, CQRS commands/queries, DTOs
- **nInvoices.Infrastructure**: Data access, EF Core, PDF generation, template engine
- **nInvoices.Api**: ASP.NET Core Web API endpoints
- **nInvoices.Web**: Vue 3 + TypeScript frontend

## 🚀 Features

- ✅ **Customer Management**: Full CRUD operations for customer data
- ✅ **Multi-Currency Support**: Handle rates in different currencies (EUR, USD, etc.)
- ✅ **Flexible Rate System**: Daily, monthly, and custom rate types per customer
- ✅ **Advanced Tax System**: Pluggable tax handlers (percentage, fixed, compound taxes)
- ✅ **Custom Invoice Templates**: Import, validate, and preview templates with placeholders
- ✅ **Monthly Invoice Generation**: Track worked days, add expenses, generate invoices
- ✅ **Custom Invoice Numbering**: Configurable formats (e.g., INV-{YEAR}-{NUMBER:000})
- ✅ **PDF Export**: Professional PDF invoices and worked days calendar
- ✅ **Multi-Language Invoices**: Localized placeholders for international clients
- ✅ **SQLite Database**: Lightweight, file-based database (easily switchable)

## 🛠️ Technology Stack

### Backend
- .NET 10 (LTS)
- ASP.NET Core Web API
- Entity Framework Core
- MediatR (CQRS pattern)
- FluentValidation
- Serilog (logging)
- QuestPDF (PDF generation)

### Frontend
- Vue 3 with Composition API
- TypeScript
- Vite
- Pinia (state management)
- Vue Router
- Axios

### Database
- SQLite (default)
- Easily switchable to PostgreSQL/SQL Server

### DevOps
- Docker & Docker Compose
- Health checks
- Structured logging

## 📋 Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (optional, for containerized deployment)

## 🚦 Getting Started

### Development Setup

1. **Clone the repository**
   ```bash
   cd nInvoices
   ```

2. **Restore backend dependencies**
   ```bash
   dotnet restore
   ```

3. **Install frontend dependencies**
   ```bash
   cd src/nInvoices.Web
   npm install
   cd ../..
   ```

4. **Run database migrations**
   ```bash
   cd src/nInvoices.Api
   dotnet ef database update
   cd ../..
   ```

5. **Start the API** (Terminal 1)
   ```bash
   cd src/nInvoices.Api
   dotnet run
   ```
   API will be available at: `https://localhost:5001`

6. **Start the frontend** (Terminal 2)
   ```bash
   cd src/nInvoices.Web
   npm run dev
   ```
   Frontend will be available at: `http://localhost:5173`

### Docker Deployment

```bash
cd docker
docker-compose up --build
```

- API: `http://localhost:5000`
- Web: `http://localhost:3000`
- Database: Volume mounted at `./docker/data`

## 🧪 Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/nInvoices.Core.Tests

# With coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportsFormat=opencover
```

## 📁 Project Structure

```
nInvoices/
├── src/
│   ├── nInvoices.Api/              # Web API
│   ├── nInvoices.Application/      # Business Logic
│   ├── nInvoices.Core/             # Domain Layer
│   ├── nInvoices.Infrastructure/   # Data Access & External Services
│   └── nInvoices.Web/              # Vue 3 Frontend
├── tests/
│   ├── nInvoices.Core.Tests/
│   ├── nInvoices.Application.Tests/
│   ├── nInvoices.Infrastructure.Tests/
│   └── nInvoices.Api.Tests/
├── docker/
│   ├── Dockerfile.api
│   ├── Dockerfile.web
│   ├── docker-compose.yml
│   └── nginx.conf
└── nInvoices.slnx
```

## 🔧 Configuration

### Database Configuration

Edit `src/nInvoices.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=nInvoices.db"
  },
  "Database": {
    "Type": "SQLite"  // Options: SQLite, PostgreSQL, SQLServer
  }
}
```

### CORS Configuration

The API is configured to allow requests from `http://localhost:5173` and `http://localhost:5174` (Vite dev servers).

## 📖 API Documentation

Once the API is running, visit:
- OpenAPI/Swagger: `https://localhost:5001/openapi/v1.json`
- Health Check: `https://localhost:5001/api/health`

## 🎨 Design Patterns Used

- **Repository Pattern**: Data access abstraction
- **Unit of Work**: Transaction management
- **CQRS**: Command-Query Responsibility Segregation
- **Strategy Pattern**: Pluggable tax calculation handlers
- **Builder Pattern**: Invoice generation
- **Factory Pattern**: Tax handler creation
- **Dependency Injection**: Throughout the application

## 🔐 Security

- Single-user application (no authentication required)
- Input validation with FluentValidation
- SQL injection protection via EF Core
- CORS configured for development

## 📝 Development Status

Phase 1: ✅ **COMPLETED**
- [x] Solution structure created
- [x] .NET 10 projects configured
- [x] Vue 3 + TypeScript frontend setup
- [x] NuGet packages installed
- [x] CORS configured
- [x] Serilog logging configured
- [x] Docker configuration created
- [x] Build successful

**Next Phase**: Domain Layer (Core Entities)

## 📄 License

MIT License - See LICENSE file for details

## 👨‍💻 Author

Developed by a freelance developer for freelance developers 🚀

## 🤝 Contributing

Contributions, issues, and feature requests are welcome!

## ⭐ Support

If you find this project useful, please consider giving it a star!


## 📚 Documentation

Comprehensive documentation is available in the Docs folder:

- **[README.md](./Docs/README.md)** - Documentation index and quick start
- **[implementation-complete-summary.md](./Docs/implementation-complete-summary.md)** - Complete system guide
- **[TEMPLATE-SYNTAX-REFERENCE.md](./Docs/TEMPLATE-SYNTAX-REFERENCE.md)** - Quick reference card
- **[phase4-implementation-complete.md](./Docs/phase4-implementation-complete.md)** - Technical details

### HTML Template System

The application now supports custom HTML invoice templates with:
- ✅ Scriban/Liquid template syntax
- ✅ Loops and conditionals
- ✅ Custom formatting functions
- ✅ Real-time validation
- ✅ PDF generation with QuestPDF

**Quick Start:**
1. Navigate to Customer → Templates
2. Click "Add Template"
3. Click "📋 Load Sample" to see example
4. Modify as needed
5. Save and generate invoices!

See **[TEMPLATE-SYNTAX-REFERENCE.md](./Docs/TEMPLATE-SYNTAX-REFERENCE.md)** for syntax guide.
