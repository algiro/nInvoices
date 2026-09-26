using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Services.Email;

public static class UserContextExtensions
{
    /// <exception cref="UnauthorizedAccessException">No authenticated user.</exception>
    public static string RequireUserId(this IUserContext userContext) =>
        string.IsNullOrWhiteSpace(userContext.UserId)
            ? throw new UnauthorizedAccessException("No authenticated user")
            : userContext.UserId;
}
