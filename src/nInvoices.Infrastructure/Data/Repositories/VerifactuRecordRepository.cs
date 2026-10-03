using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.Data.Repositories;

public sealed class VerifactuRecordRepository : Repository<VerifactuRecord>, IVerifactuRecordRepository
{
    public VerifactuRecordRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<VerifactuRecord?> GetLastAsync(CancellationToken cancellationToken = default) =>
        await _dbSet.OrderByDescending(r => r.Sequence).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<VerifactuRecord>> GetByInvoiceAsync(long invoiceId, CancellationToken cancellationToken = default) =>
        await _dbSet.Where(r => r.InvoiceId == invoiceId).OrderBy(r => r.Sequence).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<VerifactuRecord>> GetChainAsync(CancellationToken cancellationToken = default) =>
        await _dbSet.OrderBy(r => r.Sequence).ToListAsync(cancellationToken);

    // A record is part of the chain for good
    public override Task UpdateAsync(VerifactuRecord entity, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Verifactu records cannot be changed.");

    public override Task DeleteAsync(VerifactuRecord entity, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Verifactu records cannot be deleted.");
}
