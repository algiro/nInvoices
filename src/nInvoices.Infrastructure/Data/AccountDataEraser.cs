using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.Data;

/// <summary>Account deletion over EF Core: see <see cref="AccountDataDeletion.DeleteAccountDataAsync"/>.</summary>
public sealed class AccountDataEraser : IAccountDataEraser
{
    private readonly ApplicationDbContext _context;

    public AccountDataEraser(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<int> DeleteAllAsync(string userId, CancellationToken cancellationToken = default) =>
        _context.DeleteAccountDataAsync(userId, cancellationToken);
}
