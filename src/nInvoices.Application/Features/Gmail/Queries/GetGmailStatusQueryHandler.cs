using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Gmail.Queries;

public sealed class GetGmailStatusQueryHandler : IRequestHandler<GetGmailStatusQuery, GmailStatusDto>
{
    private readonly IGmailClient _gmailClient;
    private readonly IRepository<GmailConnection> _connectionRepository;
    private readonly IUserContext _userContext;

    public GetGmailStatusQueryHandler(
        IGmailClient gmailClient,
        IRepository<GmailConnection> connectionRepository,
        IUserContext userContext)
    {
        _gmailClient = gmailClient;
        _connectionRepository = connectionRepository;
        _userContext = userContext;
    }

    public async Task<GmailStatusDto> Handle(GetGmailStatusQuery request, CancellationToken cancellationToken)
    {
        var userId = _userContext.RequireUserId();
        var connection = (await _connectionRepository.FindAsync(c => c.UserId == userId, cancellationToken)).FirstOrDefault();

        return new GmailStatusDto(
            _gmailClient.IsConfigured,
            connection is not null,
            connection?.EmailAddress,
            connection?.ConnectedAt,
            connection?.LastUsedAt);
    }
}
