using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Gmail.Commands;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Account;

/// <summary>
/// Deletes all of the signed-in user's data and destroys their encryption key; it can't be undone.
/// The user confirms by typing <see cref="Confirmation"/>. The sign-in account itself is deleted by
/// Keycloak afterwards (the web app sends the user there).
/// </summary>
public sealed record DeleteAccountCommand(string? Typed) : IRequest<DeleteAccountResultDto>
{
    /// <summary>What the user types to confirm, so a stray request can't delete an account.</summary>
    public const string Confirmation = "DELETE";
}

public sealed class DeleteAccountCommandValidator : AbstractValidator<DeleteAccountCommand>
{
    public DeleteAccountCommandValidator()
    {
        RuleFor(x => x.Typed)
            .Equal(DeleteAccountCommand.Confirmation)
            .WithMessage($"Type {DeleteAccountCommand.Confirmation} to confirm.");
    }
}

public sealed class DeleteAccountCommandHandler : IRequestHandler<DeleteAccountCommand, DeleteAccountResultDto>
{
    private readonly IAccountDataEraser _eraser;
    private readonly ISender _sender;
    private readonly IUserContext _userContext;
    private readonly ILogger<DeleteAccountCommandHandler> _logger;

    public DeleteAccountCommandHandler(
        IAccountDataEraser eraser,
        ISender sender,
        IUserContext userContext,
        ILogger<DeleteAccountCommandHandler> logger)
    {
        _eraser = eraser;
        _sender = sender;
        _userContext = userContext;
        _logger = logger;
    }

    public async ValueTask<DeleteAccountResultDto> Handle(DeleteAccountCommand command, CancellationToken cancellationToken)
    {
        var userId = _userContext.RequireUserId();

        try
        {
            // Revokes the Gmail access granted to nInvoices; the stored connection goes with the rest anyway
            await _sender.Send(new DisconnectGmailCommand(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not revoke the Gmail access of user {UserId} while deleting their account", userId);
        }

        var rows = await _eraser.DeleteAllAsync(userId, cancellationToken);
        _logger.LogInformation("Deleted the account data of user {UserId}: {Rows} rows, data key destroyed", userId, rows);
        return new DeleteAccountResultDto(rows);
    }
}
