using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.Data;

/// <summary>
/// Deletes everything a user owns when they delete their account, then destroys their data key, so
/// any copy of their rows left in older database backups can no longer be decrypted.
/// </summary>
public static class AccountDataDeletion
{
    private static readonly MethodInfo DeleteOwnedMethod = typeof(AccountDataDeletion)
        .GetMethod(nameof(DeleteOwnedAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Deletes all rows of <paramref name="ownerId"/>, in one transaction, including the Verifactu
    /// records that are otherwise append-only. Safe to run again: a second run finds nothing.
    /// </summary>
    /// <returns>The number of rows deleted.</returns>
    public static async Task<int> DeleteAccountDataAsync(
        this ApplicationDbContext context,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        var order = DeletionOrder(context.Model);
        var strategy = context.Database.CreateExecutionStrategy();
        var deleted = await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            var rows = 0;
            foreach (var type in order)
            {
                await ClearSelfReferencesAsync(context, context.Model.FindEntityType(type)!, ownerId, ct);
                rows += await (Task<int>)DeleteOwnedMethod.MakeGenericMethod(type).Invoke(null, [context, ownerId, ct])!;
            }

            // Kept by user id rather than as owned data
            rows += await context.GmailConnections.Where(c => c.UserId == ownerId).ExecuteDeleteAsync(ct);
            rows += await context.OAuthStates.Where(s => s.UserId == ownerId).ExecuteDeleteAsync(ct);

            await transaction.CommitAsync(ct);
            return rows;
        }, cancellationToken);

        context.ChangeTracker.Clear();
        await context.Encryptor.DeleteKeyAsync(ownerId, cancellationToken);
        return deleted;
    }

    /// <summary>
    /// The user-data tables in an order the foreign keys allow deleting them: a table comes before
    /// every table it points to. Worked out from the model, so a new table is never forgotten.
    /// </summary>
    internal static IReadOnlyList<Type> DeletionOrder(IModel model)
    {
        var types = model.GetEntityTypes()
            .Where(t => t.BaseType is null && !t.IsOwned() && typeof(IOwnedEntity).IsAssignableFrom(t.ClrType))
            .ToList();

        // principal -> the tables pointing at it, which must be emptied first
        var dependents = types.ToDictionary(t => t, _ => new HashSet<IEntityType>());
        foreach (var type in types)
        {
            foreach (var foreignKey in type.GetForeignKeys())
            {
                var principal = foreignKey.PrincipalEntityType;
                if (principal != type && dependents.TryGetValue(principal, out var set))
                    set.Add(type);
            }
        }

        var order = new List<Type>();
        var pending = types.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        while (pending.Count > 0)
        {
            // Ready: nothing still pending points at it
            var ready = pending.FirstOrDefault(t => !dependents[t].Any(pending.Contains))
                ?? throw new InvalidOperationException(
                    "The user-data tables reference each other in a cycle: " + string.Join(", ", pending.Select(t => t.ClrType.Name)));
            order.Add(ready.ClrType);
            pending.Remove(ready);
        }
        return order;
    }

    /// <summary>
    /// A row pointing at another row of the same table (a compound tax and the tax it applies to)
    /// would block deleting that row: the reference is checked row by row, so it is cleared first.
    /// </summary>
    private static async Task ClearSelfReferencesAsync(
        ApplicationDbContext context,
        IEntityType type,
        string ownerId,
        CancellationToken cancellationToken)
    {
        var table = StoreObjectIdentifier.Table(type.GetTableName()!, type.GetSchema());
        foreach (var foreignKey in type.GetForeignKeys().Where(fk => fk.PrincipalEntityType == type))
        {
            if (foreignKey.Properties.Any(p => !p.IsNullable))
                throw new InvalidOperationException(
                    $"{type.ClrType.Name} references itself through a required column: account deletion can't clear it.");

            var assignments = string.Join(", ", foreignKey.Properties.Select(p => $"\"{p.GetColumnName(table)}\" = NULL"));
            var owner = type.FindProperty(nameof(IOwnedEntity.OwnerId))!.GetColumnName(table);
            // Names come from the model, the owner is a parameter
#pragma warning disable EF1002
            await context.Database.ExecuteSqlRawAsync(
                $"UPDATE \"{table.Name}\" SET {assignments} WHERE \"{owner}\" = {{0}}", [ownerId], cancellationToken);
#pragma warning restore EF1002
        }
    }

    private static async Task<int> DeleteOwnedAsync<TEntity>(ApplicationDbContext context, string ownerId, CancellationToken cancellationToken)
        where TEntity : class, IOwnedEntity =>
        await context.Set<TEntity>()
            .IgnoreQueryFilters()
            .Where(e => e.OwnerId == ownerId)
            .ExecuteDeleteAsync(cancellationToken);
}
