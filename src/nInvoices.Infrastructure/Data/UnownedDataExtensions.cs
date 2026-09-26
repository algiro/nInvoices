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

        var assigned = 0;
        var unowned = 0;
        foreach (var type in ownedTypes)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                unowned += await (Task<int>)CountMethod.MakeGenericMethod(type).Invoke(null, [context, cancellationToken])!;
            else
                assigned += await (Task<int>)AssignMethod.MakeGenericMethod(type).Invoke(null, [context, ownerId, cancellationToken])!;
        }

        return (assigned, unowned);
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
