# Troubleshooting Guide

## Issue: Keycloak Login Failed

### Symptoms
- Cannot login to Keycloak admin console
- "Invalid user credentials" error
- Password from `.env` file doesn't work

### Root Causes & Solutions

#### 1. Special Characters in Passwords
**Problem:** Passwords with special characters like `!` can cause issues in JDBC connection strings.

**Solution:** Use passwords without special characters for PostgreSQL and Keycloak:
- ✅ Good: `Albagnano2026Pass`, `MySecurePass2024`
- ❌ Avoid: `pass!word`, `my$ecret`, `test@123`

#### 2. .env File Location
**Problem:** Docker Compose looks for `.env` in the same directory as the `docker-compose.yml` file.

**Solution:** Ensure `.env` is in the `docker/` directory when running commands from there:
```bash
cd docker
cp ../.env .env  # Copy from root if needed
docker-compose -f docker-compose.dev.yml up -d
```

#### 3. PostgreSQL Password Not Set Correctly
**Problem:** When PostgreSQL initializes for the first time, if something goes wrong, the password might not be set correctly for network connections.

**Symptoms:**
- Password works locally but fails over network
- Keycloak can't connect despite correct environment variables

**Solution:** Manually reset the PostgreSQL password:
```bash
docker exec ninvoices-postgres-dev psql -U ninvoices_user -d postgres -c "ALTER USER ninvoices_user WITH PASSWORD 'YourPassword';"
```

#### 4. Keycloak Admin User Already Exists
**Problem:** If Keycloak was started before with different credentials, the admin user exists with unknown password.

**Solution:** Reset Keycloak database:
```bash
cd docker
docker-compose -f docker-compose.dev.yml stop keycloak
docker exec ninvoices-postgres-dev psql -U ninvoices_user -d postgres -c "DROP DATABASE IF EXISTS keycloak_db;"
docker exec ninvoices-postgres-dev psql -U ninvoices_user -d postgres -c "CREATE DATABASE keycloak_db;"
docker-compose -f docker-compose.dev.yml up -d keycloak
```

Wait 90 seconds for Keycloak to initialize, then login with credentials from `.env`.

#### 5. Health Check Failing (curl not found)
**Problem:** Keycloak container doesn't have `curl` installed, causing health checks to fail.

**Solution:** Use TCP-based health check instead (already fixed in docker-compose.dev.yml):
```yaml
healthcheck:
  test: ["CMD-SHELL", "exec 3<>/dev/tcp/localhost/8080 && echo -e 'GET / HTTP/1.1\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && timeout 1 cat <&3 | grep -q '200\\|301\\|302' || exit 1"]
```

## Quick Reset Procedure

If you encounter persistent issues, here's a complete reset:

```bash
cd docker

# Stop all services
docker-compose -f docker-compose.dev.yml down

# Clear all volumes
Remove-Item -Recurse -Force volumes\postgres-data,volumes\keycloak-data

# Verify .env file has correct passwords (no special characters)
# Edit .env if needed

# Start services
docker-compose -f docker-compose.dev.yml up -d

# Wait for services to initialize (2-3 minutes)
Start-Sleep -Seconds 120

# Check status
docker-compose -f docker-compose.dev.yml ps
```

## Testing Connectivity

### Test PostgreSQL
```bash
docker exec ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db -c "SELECT 1;"
```

### Test Keycloak Admin Login
```bash
docker exec ninvoices-keycloak-dev /opt/keycloak/bin/kcadm.sh config credentials --server http://localhost:8080 --realm master --user admin --password 'YourPassword'
```

### Test from Browser
- **Keycloak Admin:** http://localhost:8080/admin
- **Application:** http://localhost:3000
- **API Health:** http://localhost:5000/health

## Common Error Messages

### "FATAL: password authentication failed for user"
- Check if password has special characters
- Verify .env file location
- Try manually resetting the password (see section 3 above)

### "Container is unhealthy"
- Check health check configuration
- View container logs: `docker logs ninvoices-keycloak-dev`
- Ensure ports aren't already in use

### "invalid_token" - "The signature key was not found"
- API cannot reach Keycloak to get JWT signing keys
- **Check**: API must use Docker service name `keycloak` not `localhost`
- **Fix**: Update docker-compose.dev.yml:
  ```yaml
  - Keycloak__Authority=http://keycloak:8080/realms/ninvoices
  ```
- **Test**: `docker exec ninvoices-api-dev curl http://keycloak:8080`

## Getting Help

If issues persist:

1. **Check Logs:**
   ```bash
   docker logs ninvoices-keycloak-dev
   docker logs ninvoices-postgres-dev
   docker logs ninvoices-api-dev
   ```

2. **Verify Environment Variables:**
   ```bash
   docker exec ninvoices-keycloak-dev env | grep KC_
   docker exec ninvoices-postgres-dev env | grep POSTGRES_
   ```

3. **Check Network Connectivity:**
   ```bash
   docker exec ninvoices-keycloak-dev nc -zv postgres 5432
   ```

---

## Issue: Keycloak Permission Error ⚠️ PRODUCTION

### Symptoms
```
ERROR: Failed to start server in (production) mode
ERROR: ARJUNA012225: FileSystemStore::setupStore - cannot access root of object store
```

Container shows: `ninvoices-keycloak-prod Unhealthy`

### Root Cause
Keycloak runs as UID 1000, but volume is owned by root.

### Solution

```bash
cd ~/docker
sudo docker compose down
sudo chown -R 1000:1000 volumes/keycloak
sudo chmod -R 755 volumes/keycloak
sudo docker compose up -d
```

**See:** `docker/KEYCLOAK-PERMISSION-FIX.md` for detailed guide

---

## Issue: JWT Signature Validation Failed ⚠️ CRITICAL
```
Bearer error="invalid_token", error_description="The signature key was not found"
IDX10500: Signature validation failed. No security keys were provided to validate the signature.
```

API logs show:
```
Request starting HTTP/1.1 GET http://localhost:8080/realms/ninvoices/protocol/openid-connect/certs
Request finished HTTP/1.1 GET http://localhost:8080/realms/ninvoices/protocol/openid-connect/certs - 404
```

### Root Cause
**Docker networking issue** - JWT tokens contain issuer `http://localhost:8080/realms/ninvoices`, but the API container cannot reach `localhost:8080` because that's the host machine, not the Docker network. The API needs to fetch signing keys from the internal Docker address `http://keycloak:8080`.

### Solution: KeycloakBackchannelHandler (REQUIRED)

**Step 1:** Create the handler file:

`src/nInvoices.Api/Infrastructure/KeycloakBackchannelHandler.cs`
```csharp
using System.Net;

namespace nInvoices.Api.Infrastructure;

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

**Step 2:** Update `Program.cs` JWT configuration:
```csharp
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
        
        // ⚠️ CRITICAL: Use custom backchannel handler to rewrite URLs
        options.BackchannelHttpHandler = new nInvoices.Api.Infrastructure.KeycloakBackchannelHandler();
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAudiences = new[] { keycloakAudience },
            // Accept both internal (keycloak:8080) and external (localhost:8080) issuers
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
```

**Step 3:** Rebuild and restart:
```bash
cd docker
docker-compose -f docker-compose.dev.yml build api
docker-compose -f docker-compose.dev.yml up -d api
```

### How It Works
1. Browser gets JWT with issuer: `http://localhost:8080/realms/ninvoices`
2. API receives token and needs to validate signature
3. JWT middleware tries to fetch keys from issuer URL
4. **BackchannelHandler intercepts** and rewrites to: `http://keycloak:8080`
5. API successfully fetches keys from Keycloak container ✅
6. Token signature validated ✅

### Verification
```bash
# Check API logs - should see successful key fetching
docker logs ninvoices-api-dev | grep -A2 "certs"

# Should see:
# - Requests to keycloak:8080/realms/ninvoices/protocol/openid-connect/certs
# - Response 200 (not 404)
# - "Token validated for user: <user-id>"
```

### Why Other Solutions DON'T Work
- ❌ **MetadataAddress** - Ignored by JWT middleware, still uses issuer from token
- ❌ **Custom ConfigurationManager** - Keys not loaded properly
- ❌ **Changing Authority to keycloak:8080** - Causes issuer mismatch validation error
- ❌ **ValidIssuers with both URLs** - Still tries to fetch from localhost:8080
- ✅ **BackchannelHttpHandler** - Only solution that intercepts at HTTP client level

### For More Details
See `docker/KEYCLOAK-DOCKER-GUIDE.md` for complete step-by-step implementation guide.

---

## Additional Resources

4. **Review Documentation:**
   - `QUICKSTART.md` - Setup guide
   - `KEYCLOAK-DOCKER-GUIDE.md` - Complete Keycloak + Docker implementation guide
   - `README.md` - Deployment guide
   - `ENV-FILE-FAQ.md` - Environment configuration
