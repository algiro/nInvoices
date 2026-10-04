using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.Data.Repositories;

/// <summary>
/// Invoice repository implementation with support for eagerly loading related entities.
/// </summary>
public sealed class InvoiceRepository : Repository<Invoice>, IInvoiceRepository
{
    public InvoiceRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Gets an invoice by ID with TaxLines and Expenses eagerly loaded.
    /// This is needed for invoice regeneration to ensure all data is available for template rendering.
    /// </summary>
    public async Task<Invoice?> GetByIdWithRelatedAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.Invoices
            .Include(i => i.TaxLines)
            .Include(i => i.Expenses)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<InvoiceSearchResult> SearchAsync(InvoiceSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var query = await FilterAsync(_context.Invoices.AsNoTracking(), criteria, includeStatus: true, cancellationToken);
        var total = await query.CountAsync(cancellationToken);
        var skip = (criteria.Page - 1) * criteria.PageSize;

        if (criteria.Sort is InvoiceSortField.Customer or InvoiceSortField.Total)
            return new InvoiceSearchResult(await PageSortedInMemoryAsync(query, criteria, skip, cancellationToken), total);

        var items = await Sort(query, criteria.Sort, criteria.Descending)
            .Skip(skip)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return new InvoiceSearchResult(items, total);
    }

    public async Task<IReadOnlyDictionary<InvoiceStatus, int>> CountByStatusAsync(
        InvoiceSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        return await (await FilterAsync(_context.Invoices.AsNoTracking(), criteria, includeStatus: false, cancellationToken))
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);
    }

    public async Task<IReadOnlyList<InvoiceTotalsRow>> GetTotalsAsync(CancellationToken cancellationToken = default)
    {
        // Summed by the caller: SQLite can't aggregate decimals, and these rows are small
        return await _context.Invoices
            .AsNoTracking()
            .Select(i => new InvoiceTotalsRow(i.Status, i.IssueDate, i.Total.Currency, i.Total.Amount))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        return await _context.Invoices
            .Where(i => ids.Contains(i.Id))
            .ToListAsync(cancellationToken);
    }

    private async Task<IQueryable<Invoice>> FilterAsync(
        IQueryable<Invoice> query,
        InvoiceSearchCriteria criteria,
        bool includeStatus,
        CancellationToken cancellationToken)
    {
        if (includeStatus && criteria.Status is { } status)
            query = query.Where(i => i.Status == status);
        if (criteria.CustomerId is { } customerId)
            query = query.Where(i => i.CustomerId == customerId);
        if (criteria.Type is { } type)
            query = query.Where(i => i.Type == type);
        if (criteria.Year is { } year)
            query = query.Where(i => i.IssueDate.Year == year);

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            // Lower-cased on both sides: case-insensitive on SQLite and PostgreSQL alike.
            // Customer names are encrypted, so they are matched here and the query gets their ids.
            var term = criteria.Search.Trim().ToLower();
            var customerIds = (await CustomerNamesAsync(cancellationToken))
                .Where(c => c.Value.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Key)
                .ToList();
            query = query.Where(i =>
                i.Number.Value.ToLower().Contains(term) ||
                customerIds.Contains(i.CustomerId));
        }

        return query;
    }

    private async Task<Dictionary<long, string>> CustomerNamesAsync(CancellationToken cancellationToken) =>
        await _context.Customers.AsNoTracking()
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

    /// <summary>
    /// Customer names and totals are encrypted, so SQL can't order by them: the matching invoices'
    /// sort keys are read and ordered here, then only the requested page is loaded.
    /// </summary>
    private async Task<List<Invoice>> PageSortedInMemoryAsync(
        IQueryable<Invoice> query,
        InvoiceSearchCriteria criteria,
        int skip,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .Select(i => new SortRow(i.Id, i.CustomerId, i.IssueDate, i.Total.Amount))
            .ToListAsync(cancellationToken);

        IOrderedEnumerable<SortRow> ordered;
        if (criteria.Sort == InvoiceSortField.Customer)
        {
            var names = await CustomerNamesAsync(cancellationToken);
            Func<SortRow, string> name = r => names.GetValueOrDefault(r.CustomerId, "");
            ordered = criteria.Descending
                ? rows.OrderByDescending(name, StringComparer.InvariantCultureIgnoreCase)
                : rows.OrderBy(name, StringComparer.InvariantCultureIgnoreCase);
        }
        else
        {
            ordered = criteria.Descending ? rows.OrderByDescending(r => r.Total) : rows.OrderBy(r => r.Total);
        }

        // Ties are broken the same way on every page: newest first
        var pageIds = ordered.ThenByDescending(r => r.IssueDate).ThenByDescending(r => r.Id)
            .Skip(skip)
            .Take(criteria.PageSize)
            .Select(r => r.Id)
            .ToList();

        var invoices = await query.Where(i => pageIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        return pageIds.Select(id => invoices[id]).ToList();
    }

    private sealed record SortRow(long Id, long CustomerId, DateOnly IssueDate, decimal Total);

    private static IQueryable<Invoice> Sort(IQueryable<Invoice> query, InvoiceSortField sort, bool descending)
    {
        var ordered = sort switch
        {
            InvoiceSortField.Number => By(query, i => i.Number.Value, descending),
            InvoiceSortField.Period => ThenBy(By(query, i => i.Year, descending), i => i.Month, descending),
            // Lifecycle order rather than alphabetical
            InvoiceSortField.Status => By(query, i =>
                i.Status == InvoiceStatus.Draft ? 0 :
                i.Status == InvoiceStatus.Finalized ? 1 :
                i.Status == InvoiceStatus.Sent ? 2 :
                i.Status == InvoiceStatus.Paid ? 3 : 4, descending),
            _ => By(query, i => i.IssueDate, descending)
        };

        // Ties are broken the same way on every page: newest first
        return sort == InvoiceSortField.IssueDate
            ? ThenBy(ordered, i => i.Id, descending)
            : ThenBy(ThenBy(ordered, i => i.IssueDate, true), i => i.Id, true);
    }

    private static IOrderedQueryable<Invoice> By<TKey>(IQueryable<Invoice> query, Expression<Func<Invoice, TKey>> key, bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);

    private static IOrderedQueryable<Invoice> ThenBy<TKey>(IOrderedQueryable<Invoice> query, Expression<Func<Invoice, TKey>> key, bool descending) =>
        descending ? query.ThenByDescending(key) : query.ThenBy(key);
}
