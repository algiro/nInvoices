using System.Security.Cryptography;
using MediatR;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Gmail.Commands;

public sealed class DisconnectGmailCommandHandler : IRequestHandler<DisconnectGmailCommand, bool>
{
    private readonly IGmailClient _gmailClient;
    private readonly ISecretProtector _secretProtector;
    private readonly IRepository<GmailConnection> _connectionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserContext _userContext;

    public DisconnectGmailCommandHandler(
        IGmailClient gmailClient,
        ISecretProtector secretProtector,
        IRepository<GmailConnection> connectionRepository,
        IUnitOfWork unitOfWork,
        IUserContext userContext)
    {
        _gmailClient = gmailClient;
        _secretProtector = secretProtector;
        _connectionRepository = connectionRepository;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
    }

    public async Task<bool> Handle(DisconnectGmailCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContext.RequireUserId();
        var connection = (await _connectionRepository.FindAsync(c => c.UserId == userId, cancellationToken)).FirstOrDefault();
        if (connection is null)
            return false;

        try
        {
            await _gmailClient.RevokeAsync(_secretProtector.Unprotect(connection.EncryptedRefreshToken), cancellationToken);
        }
        catch (CryptographicException)
        {
            // The key that encrypted the token is gone; there is nothing left to revoke from here
        }

        await _connectionRepository.DeleteAsync(connection, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
