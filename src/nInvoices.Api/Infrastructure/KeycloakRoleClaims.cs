using System.Security.Claims;
using System.Text.Json;

namespace nInvoices.Api.Infrastructure;

/// <summary>
/// Keycloak puts realm roles in a JSON claim (<c>realm_access: { "roles": [...] }</c>) that ASP.NET Core
/// does not read. This copies them into role claims so <c>RequireRole</c> and <c>[Authorize(Roles=...)]</c> work.
/// </summary>
public static class KeycloakRoleClaims
{
    public const string RealmAccessClaim = "realm_access";

    /// <summary>
    /// The role a user needs to use the app. New users (password or Google sign-up) don't get it:
    /// an administrator grants it in Keycloak, which is how an account is approved.
    /// </summary>
    public const string AppUserRole = "user";

    public static void AddRealmRoles(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity is not ClaimsIdentity identity)
            return;

        foreach (var role in ReadRealmRoles(identity))
        {
            if (!identity.HasClaim(identity.RoleClaimType, role))
                identity.AddClaim(new Claim(identity.RoleClaimType, role));
        }
    }

    private static IEnumerable<string> ReadRealmRoles(ClaimsIdentity identity)
    {
        var roles = new List<string>();
        foreach (var claim in identity.FindAll(RealmAccessClaim))
        {
            try
            {
                using var document = JsonDocument.Parse(claim.Value);
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("roles", out var array)
                    || array.ValueKind != JsonValueKind.Array)
                    continue;

                roles.AddRange(array.EnumerateArray()
                    .Where(r => r.ValueKind == JsonValueKind.String)
                    .Select(r => r.GetString()!)
                    .Where(r => r.Length > 0));
            }
            catch (JsonException)
            {
                // A malformed claim grants nothing
            }
        }
        return roles;
    }
}
