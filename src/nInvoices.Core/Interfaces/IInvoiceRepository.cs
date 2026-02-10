namespace nInvoices.Core.Interfaces;

/// <summary>
/// Specialized repository interface for Invoice entities.
/// Extends the generic repository with invoice-specific query methods.
/// </summary>
public interface IInvoiceRepository : IRepository<Core.Entities.Invoice>
{
    /// <summary>
    /// Gets an invoice by ID with all related entities loaded (TaxLines, Expenses).
    /// </summary>
    Task<Core.Entities.Invoice?> GetByIdWithRelatedAsync(long id, CancellationToken cancellationToken = default);
}
