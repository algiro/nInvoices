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
- PostgreSQL (production - recommended)
- SQLite (development/testing)

### Authentication
- Keycloak (OAuth2/OIDC)
- JWT Bearer tokens
- Automatic token refresh

### DevOps
- Docker & Docker Compose
- Multi-stage Docker builds
- Nginx reverse proxy
- Health checks
- Structured logging
- Automated backups

## 📋 Prerequisites

### For Development
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20+](https://nodejs.org/)

### For Docker Deployment
- [Docker Engine 20.10+](https://www.docker.com/)
- [Docker Compose 2.0+](https://docs.docker.com/compose/)
- At least 2GB RAM
- At least 5GB disk space

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

### Docker Deployment with Keycloak

**NEW: The application now uses Keycloak for authentication and PostgreSQL for data storage.**

#### Quick Start

```bash
cd docker

# Copy environment file and configure
cp ../.env.example .env
# Edit .env with your settings (⚠️ avoid special characters in passwords!)

# Start all services (PostgreSQL, Keycloak, API, Web)
docker-compose -f docker-compose.dev.yml up -d

# Check services status
docker-compose -f docker-compose.dev.yml ps

# View logs
docker-compose -f docker-compose.dev.yml logs -f
```

**Access Points:**
- **Web Application**: `http://localhost:3000`
- **API**: `http://localhost:8080`
- **Keycloak Admin**: `http://localhost:8080`
- **PostgreSQL**: `localhost:5432` (via external tools)

**Default Test User:**
- Username: `testuser`
- Password: `Test123!`

#### 📚 Complete Implementation Guide

**For detailed step-by-step instructions with all issues and solutions:**

👉 **[docker/KEYCLOAK-DOCKER-GUIDE.md](./docker/KEYCLOAK-DOCKER-GUIDE.md)** 👈

This comprehensive guide covers:
- Complete setup process from scratch
- All critical issues and their solutions
- BackchannelHandler implementation (required for authentication)
- Password configuration gotchas
- Database initialization
- Keycloak configuration walkthrough
- Testing and validation
- Production considerations

#### Production Setup

```bash
cd docker

# Configure production environment
cp ../.env.example .env
# Edit .env with:
# - Strong passwords (alphanumeric only - no special characters!)
# - Your domain name
# - Production URLs (HTTPS)

# Obtain SSL certificates (Let's Encrypt recommended)
# Place certificates in docker/volumes/ssl/

# Start production stack (includes Nginx reverse proxy)
docker-compose -f docker-compose.prod.yml up -d
```

**Production URLs:**
- All services: `https://your-domain.com`
- API: `https://your-domain.com/api`
- Keycloak: `https://your-domain.com/auth`

#### Additional Documentation

- **[docker/README.md](./docker/README.md)** - Comprehensive deployment guide
- **[docker/KEYCLOAK-DOCKER-GUIDE.md](./docker/KEYCLOAK-DOCKER-GUIDE.md)** - Complete step-by-step implementation
- **[docker/TROUBLESHOOTING.md](./docker/TROUBLESHOOTING.md)** - Common issues and solutions
- **[docker/MIGRATION.md](./docker/MIGRATION.md)** - Database migration guide

#### Database Migration

If you have existing SQLite data, migrate to PostgreSQL:

```bash
cd docker
.\migrate-sqlite-to-postgres.ps1 -SqliteDbPath "../src/nInvoices.Api/nInvoices.db"
```

See **[docker/MIGRATION.md](./docker/MIGRATION.md)** for details.

#### Database Backup & Restore

```bash
# Backup
cd docker
.\backup-database.ps1

# Restore
.\restore-database.ps1 path/to/backup.sql
```

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

The application supports both SQLite (development) and PostgreSQL (production):

**SQLite (Development):**
Edit `src/nInvoices.Api/appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "Default": "Data Source=nInvoices.db"
  },
  "Database": {
    "Type": "SQLite"
  }
}
```

**PostgreSQL (Production):**
Edit `src/nInvoices.Api/appsettings.Production.json` or use environment variables:
```json
{
  "ConnectionStrings": {
    "Default": "Host=postgres;Port=5432;Database=ninvoices_db;Username=ninvoices_user;Password=your_password"
  },
  "Database": {
    "Type": "PostgreSQL"
  }
}
```

### Authentication Configuration

The application uses Keycloak for OAuth2/OIDC authentication.

**API Configuration (`appsettings.json`):**
```json
{
  "Keycloak": {
    "Authority": "http://localhost:8080/realms/ninvoices",
    "Audience": "ninvoices-api",
    "ValidIssuer": "http://localhost:8080/realms/ninvoices"
  }
}
```

**Frontend Configuration (`.env`):**
```env
VITE_KEYCLOAK_URL=http://localhost:8080
VITE_KEYCLOAK_REALM=ninvoices
VITE_KEYCLOAK_CLIENT_ID=ninvoices-web
```

### CORS Configuration

CORS origins can be configured via environment variable or appsettings:

```json
{
  "Cors": {
    "Origins": "http://localhost:3000,http://localhost:5173"
  }
}
```

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

- **OAuth2/OIDC Authentication**: Powered by Keycloak
- **JWT Bearer Tokens**: Secure API access
- **Role-Based Access Control**: User and Admin roles
- **Input validation**: FluentValidation throughout
- **SQL injection protection**: Via EF Core
- **Password Security**: Keycloak handles secure password storage
- **Session Management**: Automatic token refresh
- **CORS**: Configured for specific origins

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
