namespace nInvoices.Infrastructure.Services;

/// <summary>
/// Lets background work run as a given user: set once at the start of a scope, it makes
/// <see cref="UserContext"/> report that user instead of looking for a signed-in one, so the usual
/// per-user filtering and ownership checks apply to what the work reads and writes.
/// </summary>
public sealed class OwnerOverride
{
    public string? OwnerId { get; set; }
}
