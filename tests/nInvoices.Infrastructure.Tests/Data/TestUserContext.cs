using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.Tests.Data;

/// <summary>A fixed signed-in user (or none, with a null id) for DbContext tests.</summary>
internal sealed class TestUserContext(string? userId) : IUserContext
{
    public string? UserId { get; } = userId;
    public string? Username => UserId;
    public string? Email => null;
    public IEnumerable<string> Roles => [];
    public bool IsAuthenticated => UserId is not null;
    public bool IsInRole(string role) => false;
}
