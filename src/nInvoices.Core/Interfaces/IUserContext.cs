namespace nInvoices.Core.Interfaces;

/// <summary>
/// Interface for accessing the current authenticated user's context.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Gets the unique identifier of the current user from the authentication token.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the username/email of the current user.
    /// </summary>
    string? Username { get; }

    /// <summary>
    /// Gets the email of the current user.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Gets the roles assigned to the current user.
    /// </summary>
    IEnumerable<string> Roles { get; }

    /// <summary>
    /// Checks if the current user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Checks if the current user has a specific role.
    /// </summary>
    bool IsInRole(string role);
}
