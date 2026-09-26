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

        var query = Filter(_context.Invoices.AsNoTracking(), criteria, includeStatus: true);
        var total = await query.CountAsync(cancellationToken);

        var items = await Sort(query, criteria.Sort, criteria.Descending)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return new InvoiceSearchResult(items, total);
    }

    public async Task<IReadOnlyDictionary<InvoiceStatus, int>> CountByStatusAsync(
        InvoiceSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        return await Filter(_context.Invoices.AsNoTracking(), criteria, includeStatus: false)
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

    private static IQueryable<Invoice> Filter(IQueryable<Invoice> query, InvoiceSearchCriteria criteria, bool includeStatus)
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
            // Lower-cased on both sides: case-insensitive on SQLite and PostgreSQL alike
            var term = criteria.Search.Trim().ToLower();
            query = query.Where(i =>
                i.Number.Value.ToLower().Contains(term) ||
                i.Customer.Name.ToLower().Contains(term));
        }

        return query;
    }

    private static IQueryable<Invoice> Sort(IQueryable<Invoice> query, InvoiceSortField sort, bool descending)
    {
        var ordered = sort switch
        {
            InvoiceSortField.Number => By(query, i => i.Number.Value, descending),
            InvoiceSortField.Customer => By(query, i => i.Customer.Name, descending),
            InvoiceSortField.Period => ThenBy(By(query, i => i.Year, descending), i => i.Month, descending),
            // Lifecycle order rather than alphabetical
            InvoiceSortField.Status => By(query, i =>
                i.Status == InvoiceStatus.Draft ? 0 :
                i.Status == InvoiceStatus.Finalized ? 1 :
                i.Status == InvoiceStatus.Sent ? 2 :
                i.Status == InvoiceStatus.Paid ? 3 : 4, descending),
            // SQLite can't order by decimal; as a double the order is the same
            InvoiceSortField.Total => By(query, i => (double)i.Total.Amount, descending),
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
