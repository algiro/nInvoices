using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
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
}
