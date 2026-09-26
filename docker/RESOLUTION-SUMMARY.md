# 🎉 Authentication Issue - RESOLVED

## Status: ✅ FIXED

**Date**: February 5, 2026  
**Issue**: JWT signature validation failing in Docker environment  
**Solution**: Custom BackchannelHttpHandler

---

## Problem Summary

User was unable to authenticate after dockerizing the nInvoices application with Keycloak. The error was:

```
Bearer error="invalid_token", error_description="The signature key was not found"
```

### Root Cause

Docker networking mismatch:
- JWT tokens contained issuer: `http://localhost:8080/realms/ninvoices`
- API container tried to fetch signing keys from that URL
- API container couldn't reach `localhost:8080` (host machine)
- Needed to fetch from `http://keycloak:8080` (Docker network name)

---

## Solution Implemented

### 1. KeycloakBackchannelHandler

Created custom HTTP handler that intercepts requests and rewrites URLs:

**File**: `src/nInvoices.Api/Infrastructure/KeycloakBackchannelHandler.cs`

```csharp
public class KeycloakBackchannelHandler : HttpClientHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Rewrite localhost:8080 → keycloak:8080
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

### 2. Updated JWT Configuration

**File**: `src/nInvoices.Api/Program.cs`

```csharp
options.BackchannelHttpHandler = new nInvoices.Api.Infrastructure.KeycloakBackchannelHandler();

options.TokenValidationParameters = new TokenValidationParameters
{
    ValidIssuers = new[] 
    { 
        "http://localhost:8080/realms/ninvoices",
        keycloakAuthority
    }
};
```

---

## Verification

### Before Fix
```bash
$ docker logs ninvoices-api-dev | grep "certs"
GET http://localhost:8080/realms/ninvoices/protocol/openid-connect/certs - 404
Failed to validate the token: The signature key was not found
```

### After Fix
```bash
$ docker logs ninvoices-api-dev | grep "certs"
GET http://keycloak:8080/realms/ninvoices/protocol/openid-connect/certs - 200
Token validated for user: <user-id>
```

### Application Status

✅ All containers healthy:
- ninvoices-postgres-dev (PostgreSQL 17)
- ninvoices-keycloak-dev (Keycloak 26.0.8)
- ninvoices-api-dev (.NET 10 API)
- ninvoices-web-dev (Vue 3 frontend)

✅ Database initialized with 8 tables

✅ Keycloak realm configured with:
- API client: `ninvoices-api`
- Web client: `ninvoices-web`
- Test user: `testuser` / `Test123!`
- Audience mapper configured

✅ Authentication flow working:
- User can login via Keycloak
- JWT tokens validated successfully
- Protected API endpoints accessible

---

## Documentation Created

### Comprehensive Guides

1. **[docker/KEYCLOAK-DOCKER-GUIDE.md](./KEYCLOAK-DOCKER-GUIDE.md)** (26 KB)
   - Complete step-by-step implementation guide
   - All issues encountered with solutions
   - Architecture diagrams
   - Testing procedures
   - Production considerations

2. **[docker/JWT-AUTHENTICATION-FIX.md](./JWT-AUTHENTICATION-FIX.md)** (9 KB)
   - Detailed explanation of the networking issue
   - Why BackchannelHandler is required
   - Step-by-step implementation
   - Production configuration
   - Why other solutions don't work

3. **[docker/TROUBLESHOOTING.md](./TROUBLESHOOTING.md)** (Updated)
   - Added BackchannelHandler solution
   - Comprehensive troubleshooting steps
   - Verification procedures

4. **[DOCKERIZATION-SUMMARY.md](./DOCKERIZATION-SUMMARY.md)** (Updated)
   - Added critical fix to implementation summary
   - Updated features list

5. **[SETUP-COMPLETE.md](./SETUP-COMPLETE.md)** (Updated)
   - Added BackchannelHandler explanation
   - Updated working components list

6. **[README.md](./README.md)** (Updated)
   - Added prominent link to complete guide
   - Updated quick start instructions

---

## Issues Resolved During Implementation

### Issue 1: Special Characters in Passwords ✅
- **Problem**: JDBC connection strings broke with `!`, `@`, `#` in passwords
- **Solution**: Use alphanumeric passwords only

### Issue 2: .env File Location ✅
- **Problem**: Docker Compose couldn't find .env file
- **Solution**: Place .env in `docker/` directory

### Issue 3: PostgreSQL Password Not Set ✅
- **Problem**: Password not applied to network connections
- **Solution**: Manually reset with `ALTER USER` command

### Issue 4: Keycloak Admin Exists ✅
- **Problem**: Admin user existed with unknown password
- **Solution**: Drop and recreate `keycloak_db`

### Issue 5: Database Schema Missing ✅
- **Problem**: EF migrations were SQLite-specific
- **Solution**: Created PostgreSQL-specific schema script

### Issue 6: Audience Validation Failed ✅
- **Problem**: JWT tokens didn't include API audience
- **Solution**: Added audience mapper in Keycloak

### Issue 7: JWT Signature Validation Failed ✅
- **Problem**: Docker networking - API couldn't fetch keys
- **Solution**: BackchannelHttpHandler (this fix)

---

## Testing Checklist

✅ All containers start and reach healthy status  
✅ Database has correct schema (8 tables)  
✅ Keycloak admin console accessible  
✅ Test user can login  
✅ Frontend loads and serves application  
✅ API health check responds  
✅ API accepts authenticated requests  
✅ JWT tokens validated successfully  
✅ Protected endpoints require authentication  
✅ 401 errors handled with login redirect  

---

## Access Information

### Application URLs
- **Frontend**: http://localhost:3000
- **API**: http://localhost:8080
- **Keycloak Admin**: http://localhost:8080/admin

### Test Credentials
- **Test User**: `testuser` / `Test123!`
- **Admin**: `admin` / `Albagnano2026Pass`

### Database
- **Host**: localhost:5432
- **User**: ninvoices_user
- **Password**: Albagnano2026Pass
- **Databases**: 
  - `ninvoices_db` (application data)
  - `keycloak_db` (authentication data)

---

## Key Takeaways

1. **BackchannelHttpHandler is critical** for Docker networking with Keycloak
2. **Never use special characters** in JDBC connection passwords
3. **Audience mapper is required** for proper JWT validation
4. **.env must be in same directory** as docker-compose.yml
5. **PostgreSQL syntax differs** from SQLite - use specific schema scripts

---

## Future Recommendations

### For Production
1. Use HTTPS with valid SSL certificates
2. Generate strong passwords with `openssl rand -base64 32`
3. Configure `KC_HOSTNAME` for Keycloak
4. Set up automated database backups
5. Implement monitoring and alerting
6. Configure resource limits for containers
7. Use secrets management (AWS Secrets Manager, Azure Key Vault)

### For Development
1. Consider using docker-compose profiles for selective service startup
2. Add Adminer or pgAdmin for database management
3. Configure Keycloak realm export for version control
4. Set up pre-commit hooks for testing

---

## Resources

- [Keycloak Documentation](https://www.keycloak.org/documentation)
- [.NET JWT Bearer Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/)
- [Docker Compose Networking](https://docs.docker.com/compose/networking/)
- [OIDC Client TS](https://github.com/authts/oidc-client-ts)

---

## Support

If you encounter issues:

1. Check container logs: `docker logs ninvoices-api-dev`
2. Review `docker/TROUBLESHOOTING.md`
3. Consult `docker/KEYCLOAK-DOCKER-GUIDE.md`
4. Verify JWT token at https://jwt.io
5. Check network connectivity: `docker network inspect ninvoices-network`

---

**Issue Status**: ✅ RESOLVED  
**Authentication**: ✅ WORKING  
**Application**: ✅ FULLY OPERATIONAL
