using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace nInvoices.Infrastructure.Encryption;

/// <summary>Where the wrapped user keys live.</summary>
public interface IUserKeyStore
{
    /// <summary>
    /// Synchronous on purpose: it is called while EF reads a row (a value converter can't await).
    /// It runs once per key and process, on its own connection.
    /// </summary>
    UserKey? FindById(Guid id);

    Task<UserKey?> FindByOwnerAsync(string ownerId, CancellationToken cancellationToken);

    /// <returns>False when the owner already has a key (another request created it first).</returns>
    Task<bool> TryAddAsync(UserKey key, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserKey>> GetAllAsync(CancellationToken cancellationToken);

    Task UpdateWrappingAsync(Guid id, string masterKeyId, byte[] wrappedKey, CancellationToken cancellationToken);
}

/// <summary>
/// Reads and writes the UserKeys table through its own small context and connection, so a key can
/// be loaded in the middle of a query of <see cref="Data.ApplicationDbContext"/>.
/// </summary>
public sealed class KeyStoreDbContext(DbContextOptions<KeyStoreDbContext> options) : DbContext(options)
{
    public DbSet<UserKey> UserKeys => Set<UserKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new UserKeyConfiguration());
}

public sealed class EfUserKeyStore(DbContextOptions<KeyStoreDbContext> options) : IUserKeyStore
{
    public UserKey? FindById(Guid id)
    {
        using var db = new KeyStoreDbContext(options);
        return db.UserKeys.AsNoTracking().FirstOrDefault(k => k.Id == id);
    }

    public async Task<UserKey?> FindByOwnerAsync(string ownerId, CancellationToken cancellationToken)
    {
        await using var db = new KeyStoreDbContext(options);
        return await db.UserKeys.AsNoTracking().FirstOrDefaultAsync(k => k.OwnerId == ownerId, cancellationToken);
    }

    public async Task<bool> TryAddAsync(UserKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        await using var db = new KeyStoreDbContext(options);
        db.UserKeys.Add(key);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // The unique index on OwnerId: another request may have created this user's key first
            if (await FindByOwnerAsync(key.OwnerId, cancellationToken) is not null)
                return false;
            throw;
        }
    }

    public async Task<IReadOnlyList<UserKey>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var db = new KeyStoreDbContext(options);
        return await db.UserKeys.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task UpdateWrappingAsync(Guid id, string masterKeyId, byte[] wrappedKey, CancellationToken cancellationToken)
    {
        await using var db = new KeyStoreDbContext(options);
        var key = await db.UserKeys.FirstAsync(k => k.Id == id, cancellationToken);
        key.MasterKeyId = masterKeyId;
        key.WrappedKey = wrappedKey;
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Keeps keys in memory only: for tests and tools, never for real data.</summary>
public sealed class InMemoryUserKeyStore : IUserKeyStore
{
    private readonly ConcurrentDictionary<Guid, UserKey> _keys = new();

    public UserKey? FindById(Guid id) => _keys.GetValueOrDefault(id);

    public Task<UserKey?> FindByOwnerAsync(string ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(_keys.Values.FirstOrDefault(k => k.OwnerId == ownerId));

    public Task<bool> TryAddAsync(UserKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_keys)
        {
            if (_keys.Values.Any(k => k.OwnerId == key.OwnerId))
                return Task.FromResult(false);
            _keys[key.Id] = key;
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<UserKey>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserKey>>(_keys.Values.ToList());

    public Task UpdateWrappingAsync(Guid id, string masterKeyId, byte[] wrappedKey, CancellationToken cancellationToken)
    {
        var key = _keys[id];
        key.MasterKeyId = masterKeyId;
        key.WrappedKey = wrappedKey;
        return Task.CompletedTask;
    }
}
