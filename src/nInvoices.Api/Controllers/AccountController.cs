using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.Features.Gmail.Commands;
using nInvoices.Core.Interfaces;
using nInvoices.Infrastructure.Data;

namespace nInvoices.Api.Controllers;

/// <summary>
/// The signed-in user's own account. Deleting it removes their data from this server; the sign-in
/// account itself is deleted by Keycloak afterwards (the web app sends the user there).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    /// <summary>What the user types to confirm, so a stray request can't delete an account.</summary>
    public const string DeleteConfirmation = "DELETE";

    private readonly ApplicationDbContext _context;
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        ApplicationDbContext context,
        IMediator mediator,
        IUserContext userContext,
        ILogger<AccountController> logger)
    {
        _context = context;
        _mediator = mediator;
        _userContext = userContext;
        _logger = logger;
    }

    /// <summary>
    /// Deletes all of the user's data (customers, invoices, worked days, templates, settings, e-invoicing
    /// records...) and destroys their encryption key. It can't be undone.
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(typeof(DeleteAccountResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeleteAccountResultDto>> Delete(
        [FromBody] DeleteAccountRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request?.Confirmation != DeleteConfirmation)
            return BadRequest(new { error = $"Type {DeleteConfirmation} to confirm." });

        var userId = _userContext.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            // Revokes the Gmail access granted to nInvoices; the stored connection goes with the rest anyway
            await _mediator.Send(new DisconnectGmailCommand(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not revoke the Gmail access of user {UserId} while deleting their account", userId);
        }

        var rows = await _context.DeleteAccountDataAsync(userId, cancellationToken);
        _logger.LogInformation("Deleted the account data of user {UserId}: {Rows} rows, data key destroyed", userId, rows);

        return Ok(new DeleteAccountResultDto(rows));
    }
}

public sealed record DeleteAccountRequestDto(string? Confirmation);

public sealed record DeleteAccountResultDto(int DeletedRows);
