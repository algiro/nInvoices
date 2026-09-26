namespace nInvoices.Infrastructure.Gmail;

/// <summary>
/// Google OAuth client used to create Gmail drafts (configuration section "Gmail").
/// ClientId/ClientSecret come from user secrets in development and environment variables
/// (Gmail__ClientId, Gmail__ClientSecret) in production — never from appsettings.json.
/// </summary>
public sealed class GmailOptions
{
    public const string SectionName = "Gmail";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    /// <summary>
    /// The API's OAuth callback, exactly as registered on the Google client,
    /// e.g. http://localhost:5297/api/gmail/oauth/callback.
    /// </summary>
    public string? RedirectUri { get; set; }

    /// <summary>
    /// Where the browser lands after the callback: the web app's Settings page,
    /// e.g. http://localhost:3000/settings or https://your-domain.com/nInvoices/settings.
    /// </summary>
    public string PostConnectRedirect { get; set; } = "/settings";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(RedirectUri);
}
