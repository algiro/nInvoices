namespace nInvoices.Application.Services.Email;

/// <summary>
/// The Gmail operations the application needs: the OAuth authorization-code flow and
/// creating drafts. Implemented in Infrastructure on top of the Google API client.
/// </summary>
public interface IGmailClient
{
    /// <summary>False when no OAuth client ID/secret is configured; the feature is then unavailable.</summary>
    bool IsConfigured { get; }

    /// <summary>The Google consent URL the browser is sent to.</summary>
    string BuildAuthorizationUrl(string state, string? loginHint);

    /// <summary>Exchanges the code from Google's redirect for a refresh token.</summary>
    /// <exception cref="GmailAuthorizationException">Google rejected the code or returned no refresh token.</exception>
    Task<GmailAuthorization> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <exception cref="GmailAuthorizationException">The refresh token was revoked or has expired.</exception>
    Task<GmailDraft> CreateDraftAsync(string refreshToken, OutgoingEmail email, CancellationToken cancellationToken = default);

    /// <summary>Revokes the token at Google. Best effort: failures are logged, not thrown.</summary>
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
}

/// <param name="EmailAddress">The Gmail address that granted access.</param>
/// <param name="RefreshToken">Long-lived token used to obtain access tokens.</param>
/// <param name="Scopes">Space-separated scopes Google granted.</param>
public sealed record GmailAuthorization(string EmailAddress, string RefreshToken, string Scopes);

/// <param name="DraftId">Gmail draft id.</param>
/// <param name="MessageId">Gmail id of the draft's message (used in the "open in Gmail" link).</param>
public sealed record GmailDraft(string DraftId, string MessageId);

/// <summary>A message to put into a Gmail draft.</summary>
/// <param name="MessageId">RFC 5322 Message-ID header value, including the angle brackets.</param>
public sealed record OutgoingEmail(
    string From,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string Subject,
    string HtmlBody,
    string MessageId,
    IReadOnlyList<EmailAttachment> Attachments);

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>
/// Google refused the authorization: the code was invalid, or the stored refresh token was
/// revoked, expired or can no longer be decrypted. The user has to connect Gmail again.
/// </summary>
public sealed class GmailAuthorizationException : Exception
{
    public GmailAuthorizationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
