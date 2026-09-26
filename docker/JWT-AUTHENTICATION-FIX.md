# JWT Authentication Fix - BackchannelHttpHandler

## The Problem

When running .NET applications with Keycloak authentication in Docker, JWT token validation fails with:

```
Bearer error="invalid_token", error_description="The signature key was not found"
IDX10500: Signature validation failed. No security keys were provided to validate the signature.
```

## Root Cause

This is a **Docker networking issue**:

1. **Browser** accesses Keycloak at `http://localhost:8080` (external URL)
2. **Keycloak** issues JWT token with issuer claim: `http://localhost:8080/realms/ninvoices`
3. **Browser** sends token to API
4. **API container** receives token and needs to validate signature
5. **JWT middleware** extracts issuer from token: `http://localhost:8080/realms/ninvoices`
6. **JWT middleware** tries to fetch signing keys from: `http://localhost:8080/realms/ninvoices/protocol/openid-connect/certs`
7. **❌ FAILS**: API container cannot reach `localhost:8080` because that's the **host machine**, not the **Docker network**
8. **Result**: 404 error, no keys fetched, validation fails

## The Solution: KeycloakBackchannelHandler

Create a custom `HttpClientHandler` that intercepts requests and rewrites the hostname:

### Step 1: Create the Handler

**File**: `src/YourApi/Infrastructure/KeycloakBackchannelHandler.cs`

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
                Host = "keycloak"  // Docker service name
            };
            request.RequestUri = builder.Uri;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

### Step 2: Configure JWT Authentication

**File**: `src/YourApi/Program.cs`

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
        
        // ⚠️ CRITICAL: Use custom backchannel handler
        options.BackchannelHttpHandler = new YourApi.Infrastructure.KeycloakBackchannelHandler();
        
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
                keycloakAuthority  // http://keycloak:8080/realms/ninvoices
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

### Step 3: Deploy

```bash
cd docker
docker-compose -f docker-compose.dev.yml build api
docker-compose -f docker-compose.dev.yml up -d api
```

## How It Works

```
Browser Request (with JWT)
    ↓
API Container receives token
    ↓
JWT Middleware extracts issuer: http://localhost:8080/realms/ninvoices
    ↓
JWT Middleware tries to fetch: http://localhost:8080/realms/ninvoices/protocol/openid-connect/certs
    ↓
BackchannelHttpHandler intercepts request
    ↓
Rewrites URL to: http://keycloak:8080/realms/ninvoices/protocol/openid-connect/certs
    ↓
Request sent to Keycloak container on Docker network ✅
    ↓
Keycloak returns signing keys
    ↓
JWT Middleware validates token signature ✅
    ↓
Request authorized ✅
```

## Verification

After deploying, check API logs:

```bash
docker logs ninvoices-api-dev | grep "certs"
```

**Before fix** (broken):
```
Request starting HTTP/1.1 GET http://localhost:8080/realms/ninvoices/protocol/openid-connect/certs
Request finished HTTP/1.1 GET http://localhost:8080/realms/ninvoices/protocol/openid-connect/certs - 404
Failed to validate the token: The signature key was not found
```

**After fix** (working):
```
Request starting HTTP/1.1 GET http://keycloak:8080/realms/ninvoices/protocol/openid-connect/certs
Request finished HTTP/1.1 GET http://keycloak:8080/realms/ninvoices/protocol/openid-connect/certs - 200
Token validated for user: a1b2c3d4-5678-90ab-cdef-1234567890ab
```

## Why Other Solutions Don't Work

### ❌ Setting MetadataAddress
```csharp
options.MetadataAddress = "http://keycloak:8080/realms/ninvoices/.well-known/openid-configuration";
```
**Problem**: JWT middleware ignores this and uses issuer from token instead.

### ❌ Using Custom ConfigurationManager
```csharp
options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(...);
```
**Problem**: Keys don't load properly, still fails validation.

### ❌ Changing Authority to keycloak:8080
```csharp
options.Authority = "http://keycloak:8080/realms/ninvoices";
```
**Problem**: Causes issuer mismatch - token has `localhost:8080` but expects `keycloak:8080`.

### ❌ Adding Both to ValidIssuers
```csharp
ValidIssuers = new[] { "http://localhost:8080/realms/ninvoices", "http://keycloak:8080/realms/ninvoices" }
```
**Problem**: Still tries to fetch keys from `localhost:8080` (from token), which fails.

### ✅ BackchannelHttpHandler
**Why it works**: Intercepts at the HTTP client level BEFORE the request is sent, allowing you to rewrite the URL that the middleware tries to fetch from.

## Production Configuration

For production, adjust the handler to rewrite your public domain:

```csharp
public class KeycloakBackchannelHandler : HttpClientHandler
{
    private readonly string _internalHost;
    
    public KeycloakBackchannelHandler(string internalHost = "keycloak")
    {
        _internalHost = internalHost;
    }
    
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // In production: rewrite public hostname to internal Docker name
        if (request.RequestUri?.Host == "auth.yourdomain.com")
        {
            var builder = new UriBuilder(request.RequestUri)
            {
                Host = _internalHost,
                Port = 8080,
                Scheme = "http" // Internal communication uses HTTP
            };
            request.RequestUri = builder.Uri;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

Then register with configuration:

```csharp
var keycloakInternalHost = builder.Configuration["Keycloak:InternalHost"] ?? "keycloak";
options.BackchannelHttpHandler = new YourApi.Infrastructure.KeycloakBackchannelHandler(keycloakInternalHost);
```

## Summary

**The BackchannelHttpHandler is the ONLY solution that works reliably for JWT validation with Keycloak in Docker.**

It's a simple pattern that can be adapted to any .NET application using external identity providers in containerized environments where the external and internal addresses differ.

## Related Issues

This issue affects:
- Any .NET app with JWT authentication in Docker
- Any scenario where issuer URL in token differs from internal network address
- Microservices architectures with service mesh
- Kubernetes deployments with internal service discovery

The BackchannelHttpHandler pattern is the recommended solution for all these scenarios.
