namespace nInvoices.Core.Interfaces;

/// <summary>
/// Unit of Work pattern for managing transactions.
/// Ensures all repository operations succeed or fail together.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Saves all changes made in this unit of work to the underlying database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> in one database transaction: committed when it returns,
    /// rolled back when it throws. With a provider that retries on transient failures
    /// (PostgreSQL) the whole operation is run again, so it must be safe to repeat.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
