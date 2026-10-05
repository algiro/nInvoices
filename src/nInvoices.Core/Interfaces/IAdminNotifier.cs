namespace nInvoices.Core.Interfaces;

/// <summary>Sends short messages to the server's administrator.</summary>
public interface IAdminNotifier
{
    /// <summary>False when no channel is configured: nothing is sent.</summary>
    bool IsEnabled { get; }

    /// <returns>Whether the message was delivered. Failures are logged, never thrown.</returns>
    Task<bool> TrySendAsync(string text, CancellationToken cancellationToken = default);
}
