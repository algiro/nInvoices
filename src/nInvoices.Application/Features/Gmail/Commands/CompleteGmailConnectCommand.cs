using MediatR;

namespace nInvoices.Application.Features.Gmail.Commands;

/// <summary>
/// Handles Google's redirect back to the API after the consent screen. The request carries no
/// application login; the one-time <paramref name="State"/> identifies the user.
/// </summary>
/// <param name="Error">Set by Google when the user declined or the request failed.</param>
public sealed record CompleteGmailConnectCommand(string? Code, string? State, string? Error) : IRequest<GmailConnectOutcome>;

/// <param name="Reason">Short machine-readable reason when <paramref name="Succeeded"/> is false.</param>
public sealed record GmailConnectOutcome(bool Succeeded, string? EmailAddress, string? Reason)
{
    public const string AccessDenied = "access_denied";
    public const string InvalidState = "invalid_state";
    public const string ExpiredState = "expired_state";
    public const string ExchangeFailed = "exchange_failed";
    public const string MissingScope = "missing_scope";

    public static GmailConnectOutcome Failed(string reason) => new(false, null, reason);
}
