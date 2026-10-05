namespace nInvoices.Core.Entities;

/// <summary>
/// A signed-in account waiting for the administrator's approval (the Keycloak "user" role). One per
/// user; it remembers whether the administrator was told, so they are told once.
/// </summary>
public sealed class AccessRequest : OwnedEntityBase
{
    public string? Email { get; private set; }
    public string? Name { get; private set; }

    /// <summary>When the administrator was notified; null until a notification got through.</summary>
    public DateTime? NotifiedAt { get; private set; }

    private AccessRequest() { }

    public AccessRequest(string? email, string? name, DateTime now)
    {
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        CreatedAt = now;
    }

    public void MarkNotified(DateTime now)
    {
        NotifiedAt = now;
        UpdatedAt = now;
    }
}
