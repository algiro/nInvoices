using nInvoices.Core.Entities;

namespace nInvoices.Core.Interfaces;

/// <summary>The current user's request for access (at most one per user).</summary>
public interface IAccessRequestRepository : IRepository<AccessRequest>
{
    /// <summary>
    /// Saves a new request. False when another request of the same user was saved first (the
    /// waiting page asked twice at once); <paramref name="request"/> is then discarded.
    /// </summary>
    Task<bool> TryAddAsync(AccessRequest request, CancellationToken cancellationToken = default);
}
