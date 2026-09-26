# Complete Guide: Dockerizing .NET Apps with Keycloak Authentication

This guide documents the complete process of dockerizing a .NET application with Keycloak authentication, including all common pitfalls and their solutions based on real implementation experience.

## Table of Contents
1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Step-by-Step Implementation](#step-by-step-implementation)
4. [Critical Issues & Solutions](#critical-issues--solutions)
5. [Testing & Validation](#testing--validation)
6. [Production Considerations](#production-considerations)

---

## Overview

### What You'll Build
- Multi-container Docker application with:
  - PostgreSQL database (separate DBs for app and Keycloak)
  - Keycloak authentication server
  - .NET API with JWT Bearer authentication
  - Frontend application with OAuth2/OIDC flow
  - Data persistence via bind volumes

### Prerequisites
- Docker & Docker Compose
- .NET 8.0+ SDK
- Node.js (for frontend builds)
- Basic understanding of OAuth2/OIDC

---

## Architecture

### Container Communication Flow

```
Browser (localhost:3000)
    ↓ OAuth2 login
Keycloak (localhost:8080) ← External URL
    ↓ Issues JWT token with issuer=http://localhost:8080
Browser
    ↓ Sends JWT in Authorization header
API Container (Docker network)
    ↓ Needs to validate JWT signature
    ↓ **CRITICAL PROBLEM**: Token says issuer=localhost:8080
    ↓ But API container can't reach localhost:8080!
    ↓ **SOLUTION**: Use BackchannelHttpHandler to rewrite URLs
    ↓ Rewrites localhost:8080 → keycloak:8080
Keycloak Container (Docker network name: keycloak)
    ↓ Returns signing keys
API Container validates signature ✅
```

### Docker Network Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Docker Network: ninvoices-network                          │
│                                                               │
│  ┌──────────────┐     ┌──────────────┐     ┌─────────────┐ │
│  │  PostgreSQL  │◄────┤   Keycloak   │     │     API     │ │
│  │  :5432       │     │  :8080       │◄────┤   :8080     │ │
│  └──────────────┘     └──────────────┘     └─────────────┘ │
│         ▲                     ▲                     ▲        │
└─────────│─────────────────────│─────────────────────│────────┘
          │                     │                     │
          │                     │                     │
     Port 5432             Port 8080             Port 8080
          │                     │                     │
          └─────────────────────┴─────────────────────┘
                         Host Machine
                     (localhost / 127.0.0.1)
```

---

## Step-by-Step Implementation

### Phase 1: Environment Setup

#### 1.1 Create Directory Structure

```bash
mkdir -p docker/init-scripts
mkdir -p docker/keycloak
mkdir -p docker/volumes/postgres
mkdir -p docker/volumes/keycloak
```

#### 1.2 Create .env File

**Location:** `docker/.env` (MUST be in same directory as docker-compose.yml)

```env
# PostgreSQL Configuration
POSTGRES_USER=ninvoices_user
POSTGRES_PASSWORD=YourSecurePassword123
POSTGRES_DB=ninvoices_db

# Keycloak Configuration
KEYCLOAK_ADMIN=admin
KEYCLOAK_ADMIN_PASSWORD=YourSecurePassword123

# Database URLs
DATABASE_URL=Host=postgres;Port=5432;Database=ninvoices_db;Username=ninvoices_user;Password=YourSecurePassword123
KEYCLOAK_DATABASE_URL=Host=postgres;Port=5432;Database=keycloak_db;Username=ninvoices_user;Password=YourSecurePassword123
```

**⚠️ CRITICAL PASSWORD RULES:**
- ❌ **DO NOT** use special characters: `!`, `@`, `#`, `$`, `%`, `&`, `*`
- ✅ Use alphanumeric + basic punctuation: `letters`, `numbers`, `-`, `_`
- Reason: Special chars break JDBC connection strings in Keycloak

#### 1.3 Create Database Initialization Script

**File:** `docker/init-scripts/01-init-databases.sql`

```sql
-- Create separate databases for app and Keycloak
CREATE DATABASE ninvoices_db;
CREATE DATABASE keycloak_db;

-- Grant all privileges
GRANT ALL PRIVILEGES ON DATABASE ninvoices_db TO ninvoices_user;
GRANT ALL PRIVILEGES ON DATABASE keycloak_db TO ninvoices_user;
```

### Phase 2: Docker Compose Configuration

#### 2.1 Create docker-compose.dev.yml

**File:** `docker/docker-compose.dev.yml`

```yaml
services:
  postgres:
    image: postgres:17-alpine
    container_name: ninvoices-postgres-dev
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    volumes:
      - ./volumes/postgres:/var/lib/postgresql/data
      - ./init-scripts:/docker-entrypoint-initdb.d
    ports:
      - "5432:5432"
    networks:
      - ninvoices-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER}"]
      interval: 10s
      timeout: 5s
      retries: 5

  keycloak:
    image: quay.io/keycloak/keycloak:26.0.8
    container_name: ninvoices-keycloak-dev
    environment:
      KC_DB: postgres
      KC_DB_URL: jdbc:postgresql://postgres:5432/keycloak_db
      KC_DB_USERNAME: ${POSTGRES_USER}
      KC_DB_PASSWORD: ${POSTGRES_PASSWORD}
      KC_BOOTSTRAP_ADMIN_USERNAME: ${KEYCLOAK_ADMIN}
      KC_BOOTSTRAP_ADMIN_PASSWORD: ${KEYCLOAK_ADMIN_PASSWORD}
      KC_HOSTNAME_STRICT: false
      KC_HTTP_ENABLED: true
      KC_HEALTH_ENABLED: true
    command: start-dev
    ports:
      - "8080:8080"
    depends_on:
      postgres:
        condition: service_healthy
    networks:
      - ninvoices-network
    healthcheck:
      test: ["CMD-SHELL", "timeout 1 bash -c 'cat < /dev/null > /dev/tcp/localhost/8080' || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s

  api:
    build:
      context: ..
      dockerfile: docker/Dockerfile.api
    container_name: ninvoices-api-dev
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: ${DATABASE_URL}
      # CRITICAL: Use internal Docker network name for API
      Keycloak__Authority: http://keycloak:8080/realms/ninvoices
      Keycloak__Audience: ninvoices-api
    ports:
      - "8080:8080"
    depends_on:
      postgres:
        condition: service_healthy
      keycloak:
        condition: service_healthy
    networks:
      - ninvoices-network

  web:
    build:
      context: ..
      dockerfile: docker/Dockerfile.web
      args:
        # Use external URL for browser
        VITE_KEYCLOAK_URL: http://localhost:8080
        VITE_KEYCLOAK_REALM: ninvoices
        VITE_KEYCLOAK_CLIENT_ID: ninvoices-web
    container_name: ninvoices-web-dev
    ports:
      - "3000:3000"
    depends_on:
      - api
    networks:
      - ninvoices-network

networks:
  ninvoices-network:
    driver: bridge

volumes:
  postgres-data:
  keycloak-data:
```

**Key Points:**
- Keycloak uses `KC_BOOTSTRAP_ADMIN_*` variables (new in v26)
- API uses internal address: `http://keycloak:8080`
- Web uses external address: `http://localhost:8080`

### Phase 3: Backend - JWT Authentication Setup

#### 3.1 Install Required Packages

```bash
cd src/YourApi
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

#### 3.2 Create BackchannelHttpHandler

**⚠️ CRITICAL FIX** - This solves the Docker networking issue!

**File:** `src/YourApi/Infrastructure/KeycloakBackchannelHandler.cs`

```csharp
using System.Net;

namespace YourApi.Infrastructure;

/// <summary>
/// Custom HTTP handler that rewrites requests to localhost:8080 to use keycloak:8080 instead.
/// This solves the Docker networking issue where tokens have issuer=localhost:8080
/// but the API container needs to reach Keycloak at keycloak:8080
/// </summary>
public class KeycloakBackchannelHandler : HttpClientHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Rewrite localhost:8080 to keycloak:8080 for internal Docker communication
        if (request.RequestUri?.Host == "localhost" && request.RequestUri.Port == 8080)
        {
            var builder = new UriBuilder(request.RequestUri)
            {
                Host = "keycloak"
            };
            request.RequestUri = builder.Uri;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

**Why This Is Necessary:**
1. Browser gets JWT token with issuer: `http://localhost:8080/realms/ninvoices`
2. JWT middleware tries to fetch signing keys from issuer URL
3. API container can't reach `localhost:8080` (that's the host, not Docker network)
4. BackchannelHandler intercepts and rewrites to `http://keycloak:8080`
5. API successfully fetches keys from Keycloak container ✅

#### 3.3 Configure JWT Authentication

**File:** `src/YourApi/Program.cs`

```csharp
// Add Authentication
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        var keycloakAuthority = builder.Configuration["Keycloak:Authority"] 
            ?? throw new InvalidOperationException("Keycloak:Authority not configured");
        var keycloakAudience = builder.Configuration["Keycloak:Audience"] 
            ?? throw new InvalidOperationException("Keycloak:Audience not configured");

        options.Authority = keycloakAuthority;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        
        // ⚠️ CRITICAL: Use custom backchannel handler
        options.BackchannelHttpHandler = new YourApi.Infrastructure.KeycloakBackchannelHandler();
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAudiences = new[] { keycloakAudience },
            // Accept both internal and external issuers
            ValidIssuers = new[] 
            { 
                "http://localhost:8080/realms/ninvoices",
                keycloakAuthority
            },
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Error("Authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var userId = context.Principal?.FindFirst("sub")?.Value;
                Log.Information("Token validated for user: {UserId}", userId);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ... later in pipeline ...
app.UseAuthentication();
app.UseAuthorization();
```

#### 3.4 Update appsettings.json

```json
{
  "Keycloak": {
    "Authority": "http://keycloak:8080/realms/ninvoices",
    "Audience": "ninvoices-api"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ninvoices_db;Username=ninvoices_user;Password=password"
  }
}
```

#### 3.5 Add [Authorize] to Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize] // ← Add this
public class CustomersController : ControllerBase
{
    // Your code
}
```

### Phase 4: Frontend - OAuth2 OIDC Setup

#### 4.1 Install OIDC Client

```bash
cd src/YourWeb
npm install oidc-client-ts
```

#### 4.2 Create Auth Service

**File:** `src/services/auth.service.ts`

```typescript
import { UserManager, WebStorageStateStore } from 'oidc-client-ts';

const keycloakUrl = import.meta.env.VITE_KEYCLOAK_URL || 'http://localhost:8080';
const realm = import.meta.env.VITE_KEYCLOAK_REALM || 'ninvoices';
const clientId = import.meta.env.VITE_KEYCLOAK_CLIENT_ID || 'ninvoices-web';

const settings = {
  authority: `${keycloakUrl}/realms/${realm}`,
  client_id: clientId,
  redirect_uri: `${window.location.origin}/auth-callback`,
  post_logout_redirect_uri: window.location.origin,
  response_type: 'code',
  scope: 'openid profile email',
  userStore: new WebStorageStateStore({ store: window.localStorage }),
};

export const userManager = new UserManager(settings);

export const authService = {
  login: () => userManager.signinRedirect(),
  logout: () => userManager.signoutRedirect(),
  getUser: () => userManager.getUser(),
  handleCallback: () => userManager.signinRedirectCallback(),
};
```

#### 4.3 Add JWT Interceptor

**File:** `src/api/client.ts`

```typescript
import axios from 'axios';
import { authService } from '@/services/auth.service';

const client = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:8080',
});

// Add JWT token to requests
client.interceptors.request.use(async (config) => {
  const user = await authService.getUser();
  if (user?.access_token) {
    config.headers.Authorization = `Bearer ${user.access_token}`;
  }
  return config;
});

// Handle 401 errors
client.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response?.status === 401) {
      await authService.login();
    }
    return Promise.reject(error);
  }
);

export default client;
```

### Phase 5: Keycloak Configuration

#### 5.1 Start Containers

```bash
cd docker
docker-compose -f docker-compose.dev.yml up -d
```

Wait for all containers to be healthy (~60 seconds).

#### 5.2 Access Keycloak Admin Console

1. Open browser: http://localhost:8080
2. Click "Administration Console"
3. Login: `admin` / `YourSecurePassword123`

#### 5.3 Create Realm

1. Click "master" dropdown → "Create Realm"
2. Name: `ninvoices`
3. Enable: true
4. Click "Create"

#### 5.4 Create API Client

1. Clients → "Create client"
2. **General Settings:**
   - Client ID: `ninvoices-api`
   - Name: nInvoices API
   - Enabled: ON
3. **Capability config:**
   - Client authentication: ON
   - Authorization: OFF
   - Standard flow: OFF
   - Direct access grants: OFF
   - Service accounts roles: ON
4. Click "Save"

#### 5.5 Create Web Client

1. Clients → "Create client"
2. **General Settings:**
   - Client ID: `ninvoices-web`
   - Name: nInvoices Web
   - Enabled: ON
3. **Capability config:**
   - Client authentication: OFF
   - Standard flow: ON
   - Direct access grants: OFF
4. **Login settings:**
   - Valid redirect URIs: `http://localhost:3000/*`
   - Valid post logout redirect URIs: `http://localhost:3000`
   - Web origins: `http://localhost:3000`
5. Click "Save"

#### 5.6 Configure Audience Mapper (CRITICAL!)

Without this, tokens won't include the API audience and validation will fail.

1. Go to `ninvoices-web` client
2. Click "Client scopes" tab
3. Click `ninvoices-web-dedicated`
4. Click "Add mapper" → "By configuration"
5. Select "Audience"
6. **Configure:**
   - Name: `api-audience`
   - Included Client Audience: `ninvoices-api`
   - Add to access token: ON
7. Click "Save"

#### 5.7 Create Test User

1. Users → "Add user"
2. Username: `testuser`
3. Email: `test@example.com`
4. First name: `Test`
5. Last name: `User`
6. Email verified: ON
7. Click "Create"
8. Go to "Credentials" tab
9. Click "Set password"
10. Password: `Test123!`
11. Temporary: OFF
12. Click "Save"

### Phase 6: Database Schema Creation

#### 6.1 Create Schema Script

If using Entity Framework, you may need PostgreSQL-specific schema:

**File:** `docker/init-scripts/02-app-schema.sql`

```sql
\c ninvoices_db;

CREATE TABLE IF NOT EXISTS "Customers" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(200) NOT NULL,
    "Email" VARCHAR(200),
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Add your other tables...
```

#### 6.2 Apply Schema

```bash
# Copy schema to running container
docker cp docker/init-scripts/02-app-schema.sql ninvoices-postgres-dev:/tmp/

# Execute schema
docker exec -it ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db -f /tmp/02-app-schema.sql
```

---

## Critical Issues & Solutions

### Issue 1: "The signature key was not found"

**Symptom:**
```
Bearer error="invalid_token", error_description="The signature key was not found"
```

**Root Cause:**
- JWT token contains issuer: `http://localhost:8080/realms/ninvoices`
- API container tries to fetch signing keys from that URL
- API container cannot reach `localhost:8080` (it's the host machine)
- Request fails with 404

**Solution:**
Use `KeycloakBackchannelHandler` (see Phase 3.2) to rewrite URLs.

**Verification:**
```bash
# Check API logs - should see successful key fetching
docker logs ninvoices-api-dev | grep "certs"

# Should see requests to keycloak:8080, not localhost:8080
```

### Issue 2: Keycloak Login Failed - Password Issues

**Symptom:**
- Can't login to Keycloak with password from .env
- "Invalid credentials" error

**Causes:**

**A) Special Characters in Password:**
```env
# ❌ BAD - breaks JDBC connection
KEYCLOAK_ADMIN_PASSWORD=MyPass!2024

# ✅ GOOD - alphanumeric only
KEYCLOAK_ADMIN_PASSWORD=MyPass2024
```

**B) .env File Wrong Location:**
```bash
# ❌ BAD - Docker won't find it
/project/.env

# ✅ GOOD - same directory as docker-compose.yml
/project/docker/.env
```

**C) Admin User Already Exists:**
If Keycloak was started before with different password:

```bash
# Reset Keycloak database
docker-compose -f docker-compose.dev.yml stop keycloak
docker exec ninvoices-postgres-dev psql -U ninvoices_user -d postgres \
  -c "DROP DATABASE IF EXISTS keycloak_db;"
docker exec ninvoices-postgres-dev psql -U ninvoices_user -d postgres \
  -c "CREATE DATABASE keycloak_db;"
docker-compose -f docker-compose.dev.yml up -d keycloak
```

### Issue 3: "IDX10214: Audience validation failed"

**Symptom:**
```
IDX10214: Audience validation failed. Audiences: ''. Did not match: validationParameters.ValidAudience: 'ninvoices-api'
```

**Root Cause:**
JWT token doesn't include `aud` claim with API client ID.

**Solution:**
Add Audience Mapper (see Phase 5.6).

**Verification:**
Decode JWT token at https://jwt.io - should see:
```json
{
  "aud": ["ninvoices-api", "account"]
}
```

### Issue 4: Database Has No Tables

**Symptom:**
- API starts but returns 500 errors
- Logs show: "relation does not exist"

**Causes:**

**A) EF Migrations are SQLite-specific:**
EF Core generates SQLite-specific SQL that won't work on PostgreSQL.

**Solution:**
Create PostgreSQL-specific schema script (see Phase 6).

**B) Migrations Not Run:**
```bash
# Check if tables exist
docker exec ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db \
  -c "\dt"

# Should show list of tables
```

### Issue 5: Health Check Failing

**Symptom:**
```
Health check failed: /bin/sh: curl: not found
```

**Root Cause:**
Alpine-based images don't include curl.

**Solution:**
Use TCP-based health check:
```yaml
healthcheck:
  test: ["CMD-SHELL", "timeout 1 bash -c 'cat < /dev/null > /dev/tcp/localhost/8080' || exit 1"]
```

Or install curl:
```dockerfile
RUN apk add --no-cache curl
```

### Issue 6: CORS Errors

**Symptom:**
```
Access to XMLHttpRequest blocked by CORS policy
```

**Solution:**

**Backend (Program.cs):**
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// ... later ...
app.UseCors();
```

**Keycloak:**
Ensure Web Client has:
- Valid redirect URIs: `http://localhost:3000/*`
- Web origins: `http://localhost:3000`

### Issue 7: Token Expired / Not Refreshing

**Symptom:**
User gets logged out after 5 minutes.

**Solution:**

**Frontend:**
```typescript
// Configure automatic token refresh
const settings = {
  // ... other settings ...
  automaticSilentRenew: true,
  silent_redirect_uri: `${window.location.origin}/silent-callback`,
};
```

**Keycloak:**
1. Go to Realm Settings → Tokens
2. Increase "Access Token Lifespan": 30 minutes
3. Increase "SSO Session Idle": 8 hours

---

## Testing & Validation

### 1. Verify Container Health

```bash
docker ps --format "table {{.Names}}\t{{.Status}}"
```

All should show "healthy".

### 2. Test Keycloak

```bash
# Check Keycloak is responding
curl http://localhost:8080/realms/ninvoices/.well-known/openid-configuration

# Should return JSON with endpoints
```

### 3. Test Database

```bash
# Connect to PostgreSQL
docker exec -it ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db

# List tables
\dt

# Exit
\q
```

### 4. Test API Authentication

```bash
# Without token - should return 401
curl http://localhost:8080/api/customers

# Get token via Keycloak
TOKEN=$(curl -X POST http://localhost:8080/realms/ninvoices/protocol/openid-connect/token \
  -d "client_id=ninvoices-web" \
  -d "username=testuser" \
  -d "password=Test123!" \
  -d "grant_type=password" \
  | jq -r '.access_token')

# With token - should return 200
curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/customers
```

### 5. Test Frontend

1. Open browser: http://localhost:3000
2. Click login
3. Should redirect to Keycloak
4. Login with: `testuser` / `Test123!`
5. Should redirect back to app
6. API calls should work

### 6. Check Logs

```bash
# API logs
docker logs ninvoices-api-dev | tail -50

# Keycloak logs
docker logs ninvoices-keycloak-dev | tail -50

# Database logs
docker logs ninvoices-postgres-dev | tail -50
```

---

## Production Considerations

### 1. Use HTTPS

```yaml
# docker-compose.prod.yml
nginx:
  ports:
    - "443:443"
  volumes:
    - ./ssl:/etc/nginx/ssl
```

### 2. Secure Passwords

```bash
# Generate strong passwords
openssl rand -base64 32
```

Store in secrets management (AWS Secrets Manager, Azure Key Vault, etc.).

### 3. Configure Production Keycloak

```yaml
keycloak:
  environment:
    KC_HOSTNAME: auth.yourdomain.com
    KC_HOSTNAME_STRICT: true
    KC_HTTP_ENABLED: false
    KC_PROXY: edge
  command: start # Not start-dev
```

### 4. Database Backups

```bash
# Backup script
docker exec ninvoices-postgres-dev pg_dump -U ninvoices_user ninvoices_db > backup.sql
```

Set up automated backups with cron.

### 5. Resource Limits

```yaml
services:
  api:
    deploy:
      resources:
        limits:
          cpus: '1'
          memory: 512M
```

### 6. Monitoring

- Add health check endpoints
- Configure logging aggregation (ELK, Splunk)
- Set up alerting (Prometheus, Grafana)

### 7. Update BackchannelHandler for Production

```csharp
public class KeycloakBackchannelHandler : HttpClientHandler
{
    private readonly string _productionHost;
    
    public KeycloakBackchannelHandler(string productionHost = "keycloak")
    {
        _productionHost = productionHost;
    }
    
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // In production, rewrite public hostname to internal Docker name
        if (request.RequestUri?.Host == "auth.yourdomain.com")
        {
            var builder = new UriBuilder(request.RequestUri)
            {
                Host = _productionHost,
                Port = 8080,
                Scheme = "http" // Internal communication uses HTTP
            };
            request.RequestUri = builder.Uri;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

---

## Summary

### Key Takeaways

1. **BackchannelHttpHandler is critical** for Docker networking with Keycloak
2. **Never use special characters** in passwords for JDBC connections
3. **Audience mapper is required** for JWT validation
4. **.env location matters** - must be in same directory as docker-compose.yml
5. **Use TCP health checks** for Alpine-based images
6. **PostgreSQL syntax differs** from SQLite - create specific schema scripts

### Files You'll Create

- `docker/docker-compose.dev.yml` - Container orchestration
- `docker/.env` - Environment variables
- `docker/init-scripts/01-init-databases.sql` - Database initialization
- `docker/init-scripts/02-app-schema.sql` - Application schema
- `src/YourApi/Infrastructure/KeycloakBackchannelHandler.cs` - **Critical fix**
- `src/YourApi/Program.cs` - JWT configuration
- `src/YourWeb/src/services/auth.service.ts` - Frontend auth

### Common Commands

```bash
# Start everything
cd docker
docker-compose -f docker-compose.dev.yml up -d

# View logs
docker logs ninvoices-api-dev -f

# Restart a service
docker-compose -f docker-compose.dev.yml restart api

# Stop everything
docker-compose -f docker-compose.dev.yml down

# Reset everything (including volumes)
docker-compose -f docker-compose.dev.yml down -v
rm -rf volumes/*
```

---

## Additional Resources

- [Keycloak Documentation](https://www.keycloak.org/documentation)
- [.NET JWT Bearer](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/)
- [OIDC Client TS](https://github.com/authts/oidc-client-ts)
- [Docker Compose](https://docs.docker.com/compose/)
- [PostgreSQL Docker](https://hub.docker.com/_/postgres)

---

**Need Help?**

If you encounter issues not covered here, check:
1. Container logs: `docker logs <container-name>`
2. Health status: `docker ps`
3. Network connectivity: `docker network inspect ninvoices-network`
4. JWT token contents: https://jwt.io

**This guide was created based on real implementation experience solving production issues.**
