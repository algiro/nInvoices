# Build Issues & Resolutions

This document tracks the build issues encountered and how they were resolved.

## Issue 1: Missing Dependencies in package-lock.json

**Error:**
```
npm error `npm ci` can only install packages when your package.json and package-lock.json are in sync
npm error Missing: oidc-client-ts@3.4.1 from lock file
```

**Cause:**  
Added `oidc-client-ts` to `package.json` but didn't run `npm install` to update `package-lock.json`.

**Resolution:**
```bash
cd src/nInvoices.Web
npm install
```

**Lesson:** Always run `npm install` after adding packages to `package.json` to update the lock file before building Docker images.

---

## Issue 2: TypeScript Compilation Errors

**Error:**
```
src/router/index.ts(75,30): error TS6133: 'from' is declared but its value is never read.
src/services/auth.service.ts(99,5): error TS2322: Type 'User | null' is not assignable to type 'User'.
src/services/auth.service.ts(109,46): error TS2339: Property 'roles' does not exist on type '{}'.
```

**Cause:**  
1. Unused parameter in router navigation guard
2. Incorrect return type (should allow null)
3. Type casting needed for Keycloak custom profile properties

**Resolution:**
1. Removed unused `from` parameter from `beforeEach`
2. Changed return type to `User | null`
3. Added type cast: `(user.profile as any).realm_access?.roles`

---

## Issue 3: NuGet Package Version Not Found

**Error:**
```
error NU1102: Unable to find package Npgsql.EntityFrameworkCore.PostgreSQL with version (>= 10.0.1)
Nearest version: 10.0.0
```

**Cause:**  
Specified version `10.0.1` doesn't exist yet in NuGet.

**Resolution:**
Changed version in `nInvoices.Infrastructure.csproj` from `10.0.1` to `10.0.0`.

**Lesson:** Verify package versions exist on NuGet.org before specifying them.

---

## Issue 4: Missing HttpContextAccessor Dependency

**Error:**
```
error CS0234: The type or namespace name 'AspNetCore' does not exist in the namespace 'Microsoft'
error CS0246: The type or namespace name 'IHttpContextAccessor' could not be found
```

**Cause:**  
`UserContext` class uses `IHttpContextAccessor` which requires `Microsoft.AspNetCore.Http.Abstractions` package.

**Initial Attempt:**  
Added package to Infrastructure project, but `AddHttpContextAccessor()` extension method wasn't available.

**Resolution:**
1. Added `Microsoft.AspNetCore.Http.Abstractions` package to Infrastructure project (for types)
2. Moved `services.AddHttpContextAccessor()` registration to API project's `Program.cs` (where ASP.NET Core packages are available)
3. Kept `IUserContext` registration in Infrastructure's `DatabaseExtensions`

**Code Changes:**

`nInvoices.Infrastructure.csproj`:
```xml
<PackageReference Include="Microsoft.AspNetCore.Http.Abstractions" Version="2.2.0" />
```

`Program.cs`:
```csharp
// Add HttpContextAccessor for user context
builder.Services.AddHttpContextAccessor();

// Add Database (which registers IUserContext)
builder.Services.AddDatabase(builder.Configuration);
```

**Lesson:** Extension methods and types may come from different packages. Infrastructure layer can reference abstractions, but service registration extensions need the full ASP.NET Core stack.

---

## Issue 5: Keycloak Health Check Failures

**Error:**
```
Container ninvoices-keycloak-dev is unhealthy
dependency failed to start: container ninvoices-keycloak-dev is unhealthy
```

**Cause:**  
Complex health check command using `/health/ready` endpoint that doesn't exist in Keycloak 26.0.

**Resolution:**
Simplified health check to just test if Keycloak responds on port 8080:

```yaml
healthcheck:
  test: ["CMD-SHELL", "curl -f http://localhost:8080/ || exit 1"]
  interval: 30s
  timeout: 10s
  retries: 5
  start_period: 90s  # Increased from 60s
```

**Lesson:** Keep health checks simple. Basic connectivity tests are often sufficient.

---

## Successful Build Result

All services now build and start successfully:

```
NAME                     STATUS
ninvoices-postgres-dev   Up (healthy)
ninvoices-keycloak-dev   Up (healthy)
ninvoices-api-dev        Up (healthy)
ninvoices-web-dev        Up (healthy)
```

## Quick Reference: Build Commands

```bash
# Build all services
cd docker
docker-compose -f docker-compose.dev.yml build

# Build and start all services
docker-compose -f docker-compose.dev.yml up -d --build

# Check status
docker-compose -f docker-compose.dev.yml ps

# View logs
docker-compose -f docker-compose.dev.yml logs -f

# Stop all
docker-compose -f docker-compose.dev.yml down
```

## Troubleshooting Tips

1. **Always check package-lock.json is up to date** after changing package.json
2. **Verify package versions** exist on package registries before using them
3. **Test TypeScript compilation locally** before Docker builds
4. **Simplify health checks** - basic connectivity is usually enough
5. **Check logs** when services fail: `docker logs <container-name>`
6. **Build incrementally** - test one service at a time when troubleshooting
