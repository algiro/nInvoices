using nInvoices.Core.Interfaces;

namespace nInvoices.Core.Entities;

/// <summary>
/// Base class for entities that belong to a single user.
/// </summary>
public abstract class OwnedEntityBase : EntityBase, IOwnedEntity
{
    public string OwnerId { get; set; } = string.Empty;
}
