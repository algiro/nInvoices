using nInvoices.Core.Entities;

namespace nInvoices.Core.Interfaces;

/// <summary>
/// The current user Verifactu chain. Records can be added, never changed or deleted.
/// </summary>
public interface IVerifactuRecordRepository : IRepository<VerifactuRecord>
{
    /// <summary>The last record of the chain, or null if there is none yet.</summary>
    Task<VerifactuRecord?> GetLastAsync(CancellationToken cancellationToken = default);

    /// <summary>The records of an invoice (issued, and cancelled if it was), oldest first.</summary>
    Task<IReadOnlyList<VerifactuRecord>> GetByInvoiceAsync(long invoiceId, CancellationToken cancellationToken = default);

    /// <summary>The whole chain, in order.</summary>
    Task<IReadOnlyList<VerifactuRecord>> GetChainAsync(CancellationToken cancellationToken = default);
}
