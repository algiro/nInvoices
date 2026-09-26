namespace nInvoices.Core.Interfaces;

/// <summary>
/// Data that belongs to one user. The persistence layer stamps <see cref="OwnerId"/> on insert
/// and only ever returns rows owned by the current user.
/// </summary>
public interface IOwnedEntity
{
    /// <summary>The identity-provider subject (<c>sub</c> claim) of the owning user.</summary>
    string OwnerId { get; set; }
}
