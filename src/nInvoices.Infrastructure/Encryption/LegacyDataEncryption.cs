using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using nInvoices.Core.Interfaces;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.Services;

namespace nInvoices.Infrastructure.Encryption;

/// <summary>
/// Encrypts, in place, values stored before their column was encrypted (after an upgrade, or after
/// restoring an older backup). Runs at startup; rows already encrypted are read and left alone.
/// Rows without an owner are skipped: they are encrypted at the first startup after they get one.
/// </summary>
public static class LegacyDataEncryption
{
    private static readonly MethodInfo EncryptTypeMethod = typeof(LegacyDataEncryption)
        .GetMethod(nameof(EncryptTypeAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo OwnersMethod = typeof(LegacyDataEncryption)
        .GetMethod(nameof(OwnersOfAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <returns>The number of rows encrypted.</returns>
    public static async Task<int> EncryptLegacyDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        List<Type> types;
        var owners = new SortedSet<string>(StringComparer.Ordinal);
        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            types = context.Model.GetEntityTypes()
                .Where(t => !t.IsOwned() && typeof(IOwnedEntity).IsAssignableFrom(t.ClrType) && HasEncryptedProperties(t))
                .Select(t => t.ClrType)
                .ToList();
            foreach (var type in types)
                owners.UnionWith(await (Task<List<string>>)OwnersMethod.MakeGenericMethod(type).Invoke(null, [context, cancellationToken])!);
        }

        var encrypted = 0;
        foreach (var owner in owners)
        {
            foreach (var type in types)
            {
                // A fresh context per type and owner keeps the change tracker small
                using var scope = services.CreateScope();
                scope.ServiceProvider.GetRequiredService<OwnerOverride>().OwnerId = owner;
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.AllowRewritingVerifactuRecords = true;
                encrypted += await (Task<int>)EncryptTypeMethod.MakeGenericMethod(type).Invoke(null, [context, cancellationToken])!;
            }
        }
        return encrypted;
    }

    private static async Task<List<string>> OwnersOfAsync<TEntity>(ApplicationDbContext context, CancellationToken cancellationToken)
        where TEntity : class, IOwnedEntity =>
        await context.Set<TEntity>().IgnoreQueryFilters()
            .Where(e => e.OwnerId != "")
            .Select(e => e.OwnerId)
            .Distinct()
            .ToListAsync(cancellationToken);

    private static async Task<int> EncryptTypeAsync<TEntity>(ApplicationDbContext context, CancellationToken cancellationToken)
        where TEntity : class, IOwnedEntity
    {
        var rows = 0;
        using (var legacy = FieldEncryptor.CountLegacyReads())
        {
            // Rows are read one at a time, so the plain values counted since the previous row came
            // from this one (its owned parts are read with it, from the same table row)
            var counted = 0;
            await foreach (var entity in context.Set<TEntity>().AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                var count = legacy.Count;
                if (count == counted)
                    continue;
                counted = count;
                MarkEncryptedPropertiesModified(context.Entry(entity));
                rows++;
            }
        }

        if (rows > 0)
            await context.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
        return rows;
    }

    private static void MarkEncryptedPropertiesModified(EntityEntry entry)
    {
        foreach (var property in entry.Properties)
        {
            if (property.Metadata.GetEncryptionPurpose() is not null && property.CurrentValue is not null)
                property.IsModified = true;
        }
        foreach (var complex in entry.ComplexProperties)
            MarkEncryptedPropertiesModified(complex);
        foreach (var reference in entry.References)
        {
            if (reference.Metadata.TargetEntityType.IsOwned() && reference.TargetEntry is { } owned)
                MarkEncryptedPropertiesModified(owned);
        }
    }

    private static void MarkEncryptedPropertiesModified(ComplexPropertyEntry entry)
    {
        foreach (var property in entry.Properties)
        {
            if (property.Metadata.GetEncryptionPurpose() is not null && property.CurrentValue is not null)
                property.IsModified = true;
        }
        foreach (var nested in entry.ComplexProperties)
            MarkEncryptedPropertiesModified(nested);
    }

    private static bool HasEncryptedProperties(IReadOnlyTypeBase type) =>
        type.GetProperties().Any(p => p.GetEncryptionPurpose() is not null)
        || type.GetComplexProperties().Any(c => HasEncryptedProperties(c.ComplexType))
        || (type is IReadOnlyEntityType entity && entity.GetNavigations()
            .Any(n => n.TargetEntityType.IsOwned() && HasEncryptedProperties(n.TargetEntityType)));
}
