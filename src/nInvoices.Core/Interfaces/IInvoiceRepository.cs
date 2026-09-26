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

    /// <summary>One page of the invoices matching <paramref name="criteria"/>, in its sort order.</summary>
    Task<InvoiceSearchResult> SearchAsync(InvoiceSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many invoices match <paramref name="criteria"/> per status, ignoring its status filter
    /// (so the status tabs can show their counts).
    /// </summary>
    Task<IReadOnlyDictionary<Enums.InvoiceStatus, int>> CountByStatusAsync(InvoiceSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>Status, issue date and total of every invoice, for the list's summary figures.</summary>
    Task<IReadOnlyList<InvoiceTotalsRow>> GetTotalsAsync(CancellationToken cancellationToken = default);

    /// <summary>The invoices with these ids, in no particular order; unknown ids are ignored.</summary>
    Task<IReadOnlyList<Core.Entities.Invoice>> GetByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default);
}
