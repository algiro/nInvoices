namespace nInvoices.Core.Entities;

/// <summary>
/// A pending OAuth authorization request. Google's redirect back to the API carries no
/// application login, so the random <see cref="State"/> is what ties the callback to the
/// user who started it. Each state is short-lived and can be used once.
/// </summary>
public sealed class OAuthState : EntityBase
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public string State { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    public OAuthState()
    {
        CreatedAt = DateTime.UtcNow;
    }

    public OAuthState(string state, string userId, DateTime now) : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        State = state;
        UserId = userId;
        CreatedAt = now;
        ExpiresAt = now.Add(Lifetime);
    }

    public bool IsExpired(DateTime now) => now >= ExpiresAt;
}
