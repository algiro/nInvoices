# nInvoices Docker & Keycloak Integration - Implementation Summary

## Overview

This document summarizes the complete dockerization and Keycloak authentication integration for the nInvoices application.

## What Was Implemented

### 1. Docker Infrastructure ✅

**Files Created:**
- `docker-compose.dev.yml` - Development environment configuration
- `docker-compose.prod.yml` - Production environment with Nginx reverse proxy
- `.env.example` - Environment variables template
- `docker/init-scripts/01-init-databases.sql` - PostgreSQL initialization
- `docker/keycloak/realm-export.json` - Keycloak realm configuration
- `docker/nginx.prod.conf` - Production Nginx configuration

**Services Configured:**
- **PostgreSQL 17**: Database server with separate databases
  - `ninvoices_db` - Application data
  - `keycloak_db` - Authentication data
- **Keycloak 26.0**: OAuth2/OIDC authentication server
- **API (.NET 10)**: Backend application
- **Web (Vue 3)**: Frontend application
- **Nginx**: Reverse proxy (production only)

**Features:**
- Health checks for all services
- External bind volumes for data persistence
- Automatic migrations on startup
- Container networking with DNS resolution
- Development and production configurations

### 2. Database Migration ✅

**PostgreSQL Support Added:**
- `Npgsql.EntityFrameworkCore.PostgreSQL` package installed
- Database provider configuration updated
- Connection retry logic implemented
- Environment-specific configurations
- PostgreSQL-specific schema created (8 tables)

**Critical Fix - JWT Signature Validation:**
- Created `KeycloakBackchannelHandler` to solve Docker networking issue
- Rewrites `localhost:8080` → `keycloak:8080` for internal communication
- Enables API container to fetch signing keys from Keycloak
- **This fix is REQUIRED for authentication to work in Docker**

**Migration Tools Created:**
- `migrate-sqlite-to-postgres.ps1` - Automated migration script
- `docker/MIGRATION.md` - Detailed migration guide
- `docker/init-scripts/02-ninvoices-schema.sql` - PostgreSQL schema

**Configuration Files:**
- `appsettings.json` - Base configuration with Keycloak settings
- `appsettings.Development.json` - SQLite for local development
- `appsettings.Production.json` - PostgreSQL for production

### 3. Backend Authentication ✅

**Packages Added:**
- `Microsoft.AspNetCore.Authentication.JwtBearer` - JWT token validation

**Authentication Implementation:**
- JWT Bearer authentication configured
- Keycloak integration with OIDC discovery
- Token validation with issuer verification
- Automatic token renewal support
- Comprehensive logging

**Authorization:**
- Role-based policies (User, Admin)
- All controllers protected with `[Authorize]` attribute
- Health endpoint remains public

**User Context Service:**
- `IUserContext` interface created
- `UserContext` implementation for extracting user claims
- Integrated with dependency injection
- Access to user ID, username, email, and roles

### 4. Frontend Authentication ✅

**Package Added:**
- `oidc-client-ts@3.2.1` - OIDC/OAuth2 client library

**Files Created:**
- `src/services/auth.service.ts` - Keycloak authentication service
- `src/stores/auth.ts` - Pinia store for authentication state
- `src/views/AuthCallback.vue` - OAuth callback handler
- `src/views/SilentCallback.vue` - Silent token refresh handler

**Features Implemented:**
- PKCE flow for secure authentication
- Automatic token refresh (silent renew)
- Token injection in API requests
- Protected route navigation guards
- Session storage for return URLs
- Role-based access checks
- User profile management

**API Client Updates:**
- JWT token automatically added to requests
- 401 handling with automatic login redirect
- Token refresh on expiration

### 5. Documentation ✅

**Guides Created:**
- `docker/README.md` - Comprehensive deployment guide
  - Quick start instructions
  - Production deployment
  - Troubleshooting section
  - Security best practices
  - Monitoring and maintenance

- `docker/KEYCLOAK-DOCKER-GUIDE.md` - **Complete step-by-step implementation guide**
  - All issues encountered with solutions
  - BackchannelHandler implementation (critical fix)
  - Password configuration gotchas
  - Database initialization steps
  - Keycloak configuration walkthrough
  - Testing and validation procedures
  - Production considerations

- `docker/MIGRATION.md` - Database migration guide
  - Multiple migration options
  - Step-by-step instructions
  - Verification procedures
  - Rollback instructions

- Updated `README.md` - Main documentation
  - Docker deployment sections
  - Authentication configuration
  - Database options
  - New prerequisites

### 6. Utilities ✅

**Backup/Restore Scripts:**
- `backup-database.ps1` - Automated PostgreSQL backups
  - Timestamped backups
  - Automatic compression
  - Retention policy (keep last 10)
  - Backup size reporting

- `restore-database.ps1` - Database restoration
  - Pre-restore backup creation
  - Safety confirmations
  - Connection termination handling

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Docker Environment                        │
│                                                              │
│  ┌──────────────┐      ┌──────────────┐                    │
│  │  PostgreSQL  │◄─────│  Keycloak    │                    │
│  │   (5432)     │      │   (8080)     │                    │
│  │              │      │              │                    │
│  │ ┌──────────┐ │      │ - OAuth2     │                    │
│  │ │ninvoices_│ │      │ - OIDC       │                    │
│  │ │    db    │ │      │ - JWT tokens │                    │
│  │ └──────────┘ │      └──────────────┘                    │
│  │              │             │                             │
│  │ ┌──────────┐ │             │                             │
│  │ │keycloak_ │ │             │                             │
│  │ │    db    │ │             │                             │
│  │ └──────────┘ │             │                             │
│  └──────────────┘             │                             │
│         ▲                     ▼                             │
│         │           ┌──────────────┐                        │
│         └───────────│     API      │                        │
│                     │  (.NET 10)   │                        │
│                     │              │                        │
│                     │ - JWT Auth   │                        │
│                     │ - [Authorize]│                        │
│                     └──────────────┘                        │
│                            ▲                                │
│                            │                                │
│                     ┌──────────────┐                        │
│                     │     Web      │                        │
│                     │   (Vue 3)    │                        │
│                     │              │                        │
│                     │ - OIDC flow  │                        │
│                     │ - Auto token │                        │
│                     └──────────────┘                        │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

## Security Features

1. **OAuth2/OIDC Authentication**
   - Industry-standard authentication protocol
   - Secure token-based access
   - No password storage in application

2. **JWT Bearer Tokens**
   - Stateless authentication
   - Cryptographically signed
   - Short-lived (5 minutes) with automatic renewal

3. **PKCE Flow**
   - Protection against authorization code interception
   - No client secret needed for public clients

4. **Role-Based Access Control**
   - `user` - Standard application access
   - `admin` - Administrative functions

5. **Password Security**
   - Keycloak handles password hashing
   - Configurable password policies
   - Brute force protection

6. **Transport Security**
   - HTTPS enforced in production
   - TLS 1.2+ only
   - Secure cookie flags

## Configuration

### Environment Variables

Key configuration via `.env` file:

```env
# Database
POSTGRES_USER=ninvoices_user
POSTGRES_PASSWORD=<strong-password>
POSTGRES_DB=ninvoices_db
KEYCLOAK_DB_NAME=keycloak_db

# Keycloak
KEYCLOAK_ADMIN=admin
KEYCLOAK_ADMIN_PASSWORD=<admin-password>
KEYCLOAK_URL=http://localhost:8080
KEYCLOAK_REALM=ninvoices

# Application
API_URL=http://localhost:5000
WEB_URL=http://localhost:3000
CORS_ORIGINS=http://localhost:3000,http://localhost:5173
```

### Keycloak Realm

Pre-configured realm includes:
- Two clients: `ninvoices-api` (bearer-only), `ninvoices-web` (public)
- Two roles: `user`, `admin`
- Default admin user (password must be changed on first login)
- Token lifetimes configured
- PKCE enabled for web client

### Database Persistence

All data stored in external volumes:
- `docker/volumes/postgres/` - PostgreSQL data
- `docker/volumes/keycloak/` - Keycloak configuration
- `docker/volumes/logs/` - Application logs
- `docker/volumes/backups/` - Database backups

## Deployment

### Development

```bash
# Copy and configure environment
cp .env.example .env

# Start services
cd docker
docker-compose -f docker-compose.dev.yml up -d

# Access services
# Web: http://localhost:3000
# API: http://localhost:5000
# Keycloak: http://localhost:8080
```

### Production

```bash
# Configure for production
cp .env.example .env
# Edit .env with production settings

# Obtain SSL certificates
# Place in docker/volumes/ssl/

# Start production stack
cd docker
docker-compose -f docker-compose.prod.yml up -d

# All services accessible via:
# https://your-domain.com
```

## Testing & Validation

### Health Checks

```bash
# API
curl http://localhost:5000/api/health

# Keycloak
curl http://localhost:8080/health/ready

# PostgreSQL
docker exec ninvoices-postgres-dev pg_isready -U ninvoices_user
```

### Authentication Flow

1. User visits app → redirected to Keycloak login
2. User authenticates with Keycloak
3. Keycloak issues authorization code
4. App exchanges code for tokens (PKCE)
5. Access token used for API requests
6. Token automatically refreshed before expiry

### Database Verification

```bash
# Connect to PostgreSQL
docker exec -it ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db

# List tables
\dt

# Check data
SELECT COUNT(*) FROM "Customers";

# Exit
\q
```

## Maintenance

### Backups

Automated backup script:
```bash
cd docker
.\backup-database.ps1
```

Creates timestamped, compressed backups in `volumes/backups/`

### Restore

```bash
cd docker
.\restore-database.ps1 volumes/backups/ninvoices-backup-YYYY-MM-DD.sql
```

### Updates

```bash
# Pull latest changes
git pull

# Rebuild and restart
cd docker
docker-compose -f docker-compose.dev.yml up -d --build
```

### Logs

```bash
# All services
docker-compose -f docker-compose.dev.yml logs -f

# Specific service
docker-compose -f docker-compose.dev.yml logs -f api
```

## Troubleshooting

Common issues and solutions documented in:
- `docker/README.md` - Deployment troubleshooting
- `docker/MIGRATION.md` - Migration issues

## Next Steps

The application is now ready for:

1. **Deployment** - Follow docker/README.md for your environment
2. **Migration** - Use migration scripts if you have existing data
3. **Customization** - Adjust Keycloak realm settings as needed
4. **Monitoring** - Set up logging/monitoring solutions
5. **Backup Strategy** - Implement automated backup schedule

## Resources

- **Keycloak Documentation**: https://www.keycloak.org/documentation
- **PostgreSQL Documentation**: https://www.postgresql.org/docs/
- **Docker Documentation**: https://docs.docker.com/
- **OAuth2/OIDC Specs**: https://oauth.net/2/

## Support

For issues or questions:
1. Check relevant documentation (docker/README.md, docker/MIGRATION.md)
2. Review logs for error messages
3. Search existing GitHub issues
4. Create new issue with reproduction steps and logs (sanitize sensitive data)
