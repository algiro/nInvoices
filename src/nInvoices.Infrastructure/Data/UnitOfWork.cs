using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.Data;

/// <summary>
/// Unit of Work pattern implementation.
/// Manages database transactions and coordinates Repository operations.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // A retrying execution strategy (PostgreSQL) rejects a transaction started outside it:
        // the strategy has to run the whole transaction as one retriable unit
        var strategy = _context.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async ct =>
        {
            // Disposed without a commit, the transaction is rolled back
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            await operation(ct);
            await transaction.CommitAsync(ct);
        }, cancellationToken);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
