using nInvoices.Core.Entities;

namespace nInvoices.Core.Interfaces;

/// <summary>
/// Specialized repository interface for WorkDay entities.
/// Extends the generic repository with queries that eagerly load project allocations.
/// </summary>
public interface IWorkDayRepository : IRepository<WorkDay>
{
    /// <summary>
    /// Gets all work days for a customer within the given month, with their
    /// project allocations (and each allocation's project) eagerly loaded.
    /// </summary>
    Task<IReadOnlyList<WorkDay>> GetByCustomerAndMonthAsync(
        long customerId,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}
