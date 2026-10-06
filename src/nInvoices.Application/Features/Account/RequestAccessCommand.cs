using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nInvoices.Application.DTOs;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Account;

/// <summary>
/// Sent by the "waiting for approval" page: records that the signed-in account needs the
/// <see cref="AppRoles.User"/> role and tells the administrator, once per account. Approved users have
/// nothing to request. <paramref name="DisplayName"/> is the person's name from the sign-in, if any.
/// </summary>
public sealed record RequestAccessCommand(string? DisplayName) : IRequest<AccessRequestResultDto>;

public sealed class RequestAccessCommandHandler : IRequestHandler<RequestAccessCommand, AccessRequestResultDto>
{
    private readonly IAccessRequestRepository _requests;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserContext _userContext;
    private readonly IAdminNotifier _notifier;
    private readonly NotificationOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<RequestAccessCommandHandler> _logger;

    public RequestAccessCommandHandler(
        IAccessRequestRepository requests,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IAdminNotifier notifier,
        IOptions<NotificationOptions> options,
        TimeProvider time,
        ILogger<RequestAccessCommandHandler> logger)
    {
        _requests = requests;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _notifier = notifier;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    public async ValueTask<AccessRequestResultDto> Handle(RequestAccessCommand command, CancellationToken cancellationToken)
    {
        var userId = _userContext.RequireUserId();
        if (_userContext.IsInRole(AppRoles.User))
            return new AccessRequestResultDto(Approved: true, AdministratorNotified: false);

        var now = _time.GetUtcNow().UtcDateTime;
        var request = (await _requests.GetAllAsync(cancellationToken)).FirstOrDefault();
        if (request is null)
        {
            request = new AccessRequest(_userContext.Email, command.DisplayName ?? _userContext.Username, now);
            if (!await _requests.TryAddAsync(request, cancellationToken))
            {
                // The page asked twice at once: the other request recorded it (and notifies)
                return new AccessRequestResultDto(Approved: false, AdministratorNotified: _notifier.IsEnabled);
            }
            _logger.LogInformation("Account {UserId} is waiting for approval", userId);
        }

        if (request.NotifiedAt is null && await _notifier.TrySendAsync(Message(userId, request), cancellationToken))
        {
            request.MarkNotified(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new AccessRequestResultDto(Approved: false, AdministratorNotified: request.NotifiedAt is not null);
    }

    private string Message(string userId, AccessRequest request)
    {
        var who = (request.Name, request.Email) switch
        {
            ({ } n, { } e) when !string.Equals(n, e, StringComparison.OrdinalIgnoreCase) => $"{n} <{e}>",
            (_, { } e) => e,
            ({ } n, _) => n,
            _ => userId
        };
        var lines = new List<string> { "nInvoices: a new account is waiting for approval", who };
        if (!string.IsNullOrWhiteSpace(_options.KeycloakUserUrl))
        {
            var url = _options.KeycloakUserUrl.Replace("{userId}", Uri.EscapeDataString(userId), StringComparison.Ordinal);
            lines.Add($"Approve it (Role mapping > Assign role > user): {url}");
        }
        return string.Join('\n', lines);
    }
}
