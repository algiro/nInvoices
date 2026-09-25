using System.Buffers.Text;
using System.Security.Cryptography;
using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Gmail.Commands;

public sealed class StartGmailConnectCommandHandler : IRequestHandler<StartGmailConnectCommand, GmailConnectUrlDto>
{
    private readonly IGmailClient _gmailClient;
    private readonly IRepository<OAuthState> _stateRepository;
    private readonly IRepository<GmailConnection> _connectionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserContext _userContext;
    private readonly TimeProvider _timeProvider;

    public StartGmailConnectCommandHandler(
        IGmailClient gmailClient,
        IRepository<OAuthState> stateRepository,
        IRepository<GmailConnection> connectionRepository,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider)
    {
        _gmailClient = gmailClient;
        _stateRepository = stateRepository;
        _connectionRepository = connectionRepository;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _timeProvider = timeProvider;
    }

    public async Task<GmailConnectUrlDto> Handle(StartGmailConnectCommand request, CancellationToken cancellationToken)
    {
        if (!_gmailClient.IsConfigured)
            throw new InvoiceEmailException(InvoiceEmailException.GmailNotConfigured, "Gmail is not configured on the server.");

        var userId = _userContext.RequireUserId();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Abandoned attempts (consent screen closed) leave states behind; clear them as we go
        foreach (var expired in await _stateRepository.FindAsync(s => s.ExpiresAt <= now, cancellationToken))
            await _stateRepository.DeleteAsync(expired, cancellationToken);

        var state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        await _stateRepository.AddAsync(new OAuthState(state, userId, now), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reconnecting preselects the account already in use
        var existing = (await _connectionRepository.FindAsync(c => c.UserId == userId, cancellationToken)).FirstOrDefault();

        return new GmailConnectUrlDto(_gmailClient.BuildAuthorizationUrl(state, existing?.EmailAddress));
    }
}
