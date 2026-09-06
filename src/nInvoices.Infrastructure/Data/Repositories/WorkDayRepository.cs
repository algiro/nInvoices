using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.Data.Repositories;

/// <summary>
/// WorkDay repository implementation with support for eagerly loading project allocations.
/// </summary>
public sealed class WorkDayRepository : Repository<WorkDay>, IWorkDayRepository
{
    public WorkDayRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<WorkDay>> GetByCustomerAndMonthAsync(
        long customerId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var startDate = new DateOnly(year, month, 1);
        var endDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        return await _context.WorkDays
            .Include(wd => wd.Projects)
                .ThenInclude(p => p.Project)
            .Where(wd => wd.CustomerId == customerId && wd.Date >= startDate && wd.Date <= endDate)
            .ToListAsync(cancellationToken);
    }
}
