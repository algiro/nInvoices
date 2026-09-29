using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.Data;

/// <summary>
/// Handles rows created before data was scoped per user. They have an empty owner, so no
/// user can see them until they are assigned to someone.
/// </summary>
public static class UnownedDataExtensions
{
    private static readonly MethodInfo AssignMethod = typeof(UnownedDataExtensions)
        .GetMethod(nameof(AssignAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo CountMethod = typeof(UnownedDataExtensions)
        .GetMethod(nameof(CountAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Gives every unowned row to <paramref name="ownerId"/> when it is set, otherwise just counts them.
    /// </summary>
    /// <returns>The number of rows assigned, and the number still without an owner.</returns>
    public static async Task<(int Assigned, int Unowned)> AssignUnownedDataAsync(
        this IServiceProvider services,
        string? ownerId,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.AssignUnownedDataAsync(ownerId, cancellationToken);
    }

    public static async Task<(int Assigned, int Unowned)> AssignUnownedDataAsync(
        this ApplicationDbContext context,
        string? ownerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ownedTypes = context.Model.GetEntityTypes()
            .Where(t => t.BaseType is null && !t.IsOwned() && typeof(IOwnedEntity).IsAssignableFrom(t.ClrType))
            .Select(t => t.ClrType)
            .ToList();

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            var unowned = 0;
            foreach (var type in ownedTypes)
                unowned += await (Task<int>)CountMethod.MakeGenericMethod(type).Invoke(null, [context, cancellationToken])!;
            return (0, unowned);
        }

        // All or nothing: a failure halfway would leave the data split between owners
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            await ResolveConflictsAsync(context, ownerId, ct);

            var assigned = 0;
            foreach (var type in ownedTypes)
                assigned += await (Task<int>)AssignMethod.MakeGenericMethod(type).Invoke(null, [context, ownerId, ct])!;

            await transaction.CommitAsync(ct);
            return (assigned, 0);
        }, cancellationToken);
    }

    /// <summary>
    /// Clears rows the owner already has that would clash with the legacy ones on a per-user
    /// unique index. They exist when the user signed in before the legacy data was assigned:
    /// the app then created defaults for them. The legacy rows hold the real history, so they win.
    /// </summary>
    private static async Task ResolveConflictsAsync(
        ApplicationDbContext context,
        string ownerId,
        CancellationToken cancellationToken)
    {
        // One calendar per country: drop the owner's (built-in) one; its rules cascade
        var legacyCountries = await context.HolidayCalendars.IgnoreQueryFilters()
            .Where(c => c.OwnerId == string.Empty)
            .Select(c => c.CountryCode)
            .ToListAsync(cancellationToken);
        if (legacyCountries.Count > 0)
            await context.HolidayCalendars.IgnoreQueryFilters()
                .Where(c => c.OwnerId == ownerId && legacyCountries.Contains(c.CountryCode))
                .ExecuteDeleteAsync(cancellationToken);

        // One sequence per user: keep the legacy one, never below the owner's own counter,
        // so no invoice number is handed out twice
        var ownValue = await context.InvoiceSequences.IgnoreQueryFilters()
            .Where(s => s.OwnerId == ownerId)
            .Select(s => (int?)s.CurrentValue)
            .FirstOrDefaultAsync(cancellationToken);
        if (ownValue is int value &&
            await context.InvoiceSequences.IgnoreQueryFilters().AnyAsync(s => s.OwnerId == string.Empty, cancellationToken))
        {
            await context.InvoiceSequences.IgnoreQueryFilters()
                .Where(s => s.OwnerId == string.Empty && s.CurrentValue < value)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.CurrentValue, value), cancellationToken);
            await context.InvoiceSequences.IgnoreQueryFilters()
                .Where(s => s.OwnerId == ownerId)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private static Task<int> AssignAsync<TEntity>(
        ApplicationDbContext context,
        string ownerId,
        CancellationToken cancellationToken) where TEntity : class, IOwnedEntity =>
        context.Set<TEntity>()
            .IgnoreQueryFilters()
            .Where(e => e.OwnerId == string.Empty)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.OwnerId, ownerId), cancellationToken);

    private static Task<int> CountAsync<TEntity>(
        ApplicationDbContext context,
        CancellationToken cancellationToken) where TEntity : class, IOwnedEntity =>
        context.Set<TEntity>()
            .IgnoreQueryFilters()
            .CountAsync(e => e.OwnerId == string.Empty, cancellationToken);
}
