using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.Data.Repositories;

public sealed class AccessRequestRepository : Repository<AccessRequest>, IAccessRequestRepository
{
    public AccessRequestRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<bool> TryAddAsync(AccessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Entities.Add(request);
        try
        {
            await Context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // One request per user (unique index): a concurrent call saved its own first
            Context.Entry(request).State = EntityState.Detached;
            return false;
        }
    }
}
