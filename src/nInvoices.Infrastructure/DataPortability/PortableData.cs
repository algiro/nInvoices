using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;

namespace nInvoices.Infrastructure.DataPortability;

/// <summary>What the exports share: rates referred to by position, and expenses.</summary>
internal static class PortableData
{
    /// <summary>Each customer's rate ids in id order, the order rates are exported in.</summary>
    public static async Task<Dictionary<long, List<long>>> RatesInIdOrderAsync(
        ApplicationDbContext context,
        IReadOnlyCollection<long> customerIds,
        CancellationToken cancellationToken) =>
        (await context.Rates.AsNoTracking()
            .Where(r => customerIds.Contains(r.CustomerId))
            .Select(r => new { r.Id, r.CustomerId })
            .ToListAsync(cancellationToken))
        .GroupBy(r => r.CustomerId)
        .ToDictionary(g => g.Key, g => g.Select(r => r.Id).Order().ToList());

    public static int? IndexOf(List<long> rateIds, long? rateId) =>
        rateId is { } id && rateIds.IndexOf(id) is var index and >= 0 ? index : null;

    public static long? RateAt(List<long> rateIds, int? index) =>
        index is { } i && i >= 0 && i < rateIds.Count ? rateIds[i] : null;

    public static ExpenseDto ToExpenseDto(Expense e) => new()
    {
        Description = e.Description,
        Amount = e.Amount.Amount,
        Currency = e.Amount.Currency,
        Date = e.Date
    };
}
