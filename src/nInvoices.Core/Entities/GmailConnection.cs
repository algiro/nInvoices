namespace nInvoices.Core.Entities;

/// <summary>
/// A user's authorization to create drafts in their Gmail account.
/// The refresh token is stored encrypted; it is decrypted only to call the Gmail API.
/// </summary>
public sealed class GmailConnection : EntityBase
{
    /// <summary>Id of the application user (the Keycloak subject) who connected the account.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The Gmail address the drafts are created in.</summary>
    public string EmailAddress { get; set; } = string.Empty;

    public string EncryptedRefreshToken { get; set; } = string.Empty;

    /// <summary>Space-separated OAuth scopes Google granted.</summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>When the current authorization was granted (first connection or last reconnect).</summary>
    public DateTime ConnectedAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public GmailConnection()
    {
        CreatedAt = DateTime.UtcNow;
    }

    public GmailConnection(string userId, string emailAddress, string encryptedRefreshToken, string scopes) : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        UserId = userId;
        Reconnect(emailAddress, encryptedRefreshToken, scopes);
    }

    /// <summary>Replaces the stored authorization after the user connected again.</summary>
    public void Reconnect(string emailAddress, string encryptedRefreshToken, string scopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedRefreshToken);

        EmailAddress = emailAddress;
        EncryptedRefreshToken = encryptedRefreshToken;
        Scopes = scopes ?? string.Empty;
        ConnectedAt = DateTime.UtcNow;
        UpdatedAt = ConnectedAt;
    }

    public void MarkUsed()
    {
        LastUsedAt = DateTime.UtcNow;
    }
}
