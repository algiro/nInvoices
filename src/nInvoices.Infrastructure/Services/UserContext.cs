using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.Services;

/// <summary>
/// Implementation of IUserContext that extracts user information from JWT claims.
/// </summary>
public sealed class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly OwnerOverride? _ownerOverride;

    public UserContext(IHttpContextAccessor httpContextAccessor, OwnerOverride? ownerOverride = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _ownerOverride = ownerOverride;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    // Background work runs as the user it was set to; a request, as the signed-in one
    public string? UserId => _ownerOverride?.OwnerId
        ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User?.FindFirst("sub")?.Value;

    public string? Username => User?.FindFirst("preferred_username")?.Value 
        ?? User?.FindFirst(ClaimTypes.Name)?.Value;

    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value 
        ?? User?.FindFirst("email")?.Value;

    public IEnumerable<string> Roles => User?.FindAll(ClaimTypes.Role)
        .Select(c => c.Value) 
        ?? User?.FindAll("realm_access")
            .SelectMany(c => 
            {
                // Parse Keycloak realm roles from JWT
                var json = System.Text.Json.JsonDocument.Parse(c.Value);
                return json.RootElement.GetProperty("roles")
                    .EnumerateArray()
                    .Select(r => r.GetString() ?? string.Empty);
            })
        ?? Enumerable.Empty<string>();

    public bool IsAuthenticated => !string.IsNullOrEmpty(_ownerOverride?.OwnerId) || (User?.Identity?.IsAuthenticated ?? false);

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
