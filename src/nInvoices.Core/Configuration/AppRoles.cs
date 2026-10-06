namespace nInvoices.Core.Configuration;

/// <summary>The roles the app checks (Keycloak realm roles).</summary>
public static class AppRoles
{
    /// <summary>
    /// The role a user needs to use the app. New users (password or Google sign-up) don't get it:
    /// an administrator grants it in Keycloak, which is how an account is approved.
    /// </summary>
    public const string User = "user";

    public const string Admin = "admin";
}
