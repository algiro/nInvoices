namespace nInvoices.Application.Services.Email;

public static class GmailScopes
{
    /// <summary>Create, read, update and send drafts. The only Gmail permission the app asks for.</summary>
    public const string Compose = "https://www.googleapis.com/auth/gmail.compose";

    public static bool Includes(string grantedScopes, string scope) =>
        grantedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(scope, StringComparer.Ordinal);
}
