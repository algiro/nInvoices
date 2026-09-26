using MediatR;
using Microsoft.Extensions.Logging;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Gmail.Commands;

public sealed class CompleteGmailConnectCommandHandler : IRequestHandler<CompleteGmailConnectCommand, GmailConnectOutcome>
{
    private readonly IGmailClient _gmailClient;
    private readonly ISecretProtector _secretProtector;
    private readonly IRepository<OAuthState> _stateRepository;
    private readonly IRepository<GmailConnection> _connectionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CompleteGmailConnectCommandHandler> _logger;

    public CompleteGmailConnectCommandHandler(
        IGmailClient gmailClient,
        ISecretProtector secretProtector,
        IRepository<OAuthState> stateRepository,
        IRepository<GmailConnection> connectionRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<CompleteGmailConnectCommandHandler> logger)
    {
        _gmailClient = gmailClient;
        _secretProtector = secretProtector;
        _stateRepository = stateRepository;
        _connectionRepository = connectionRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<GmailConnectOutcome> Handle(CompleteGmailConnectCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.State))
            return GmailConnectOutcome.Failed(GmailConnectOutcome.InvalidState);

        var pending = (await _stateRepository.FindAsync(s => s.State == request.State, cancellationToken)).FirstOrDefault();
        if (pending is null)
            return GmailConnectOutcome.Failed(GmailConnectOutcome.InvalidState);

        // One use only, whatever happens next
        await _stateRepository.DeleteAsync(pending, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (pending.IsExpired(_timeProvider.GetUtcNow().UtcDateTime))
            return GmailConnectOutcome.Failed(GmailConnectOutcome.ExpiredState);

        if (!string.IsNullOrWhiteSpace(request.Error) || string.IsNullOrWhiteSpace(request.Code))
        {
            _logger.LogInformation("Gmail connection declined for user {UserId}: {Error}", pending.UserId, request.Error);
            return GmailConnectOutcome.Failed(GmailConnectOutcome.AccessDenied);
        }

        GmailAuthorization authorization;
        try
        {
            authorization = await _gmailClient.ExchangeCodeAsync(request.Code, cancellationToken);
        }
        catch (GmailAuthorizationException ex)
        {
            _logger.LogWarning(ex, "Gmail code exchange failed for user {UserId}", pending.UserId);
            return GmailConnectOutcome.Failed(GmailConnectOutcome.ExchangeFailed);
        }

        // The consent screen lets the user untick individual permissions
        if (!GmailScopes.Includes(authorization.Scopes, GmailScopes.Compose))
        {
            await _gmailClient.RevokeAsync(authorization.RefreshToken, cancellationToken);
            return GmailConnectOutcome.Failed(GmailConnectOutcome.MissingScope);
        }

        var encrypted = _secretProtector.Protect(authorization.RefreshToken);
        var existing = (await _connectionRepository.FindAsync(c => c.UserId == pending.UserId, cancellationToken)).FirstOrDefault();
        if (existing is null)
        {
            await _connectionRepository.AddAsync(
                new GmailConnection(pending.UserId, authorization.EmailAddress, encrypted, authorization.Scopes),
                cancellationToken);
        }
        else
        {
            existing.Reconnect(authorization.EmailAddress, encrypted, authorization.Scopes);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Gmail account {Email} connected for user {UserId}", authorization.EmailAddress, pending.UserId);

        return new GmailConnectOutcome(true, authorization.EmailAddress, null);
    }
}
