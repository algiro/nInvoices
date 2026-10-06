using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Account;

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
    private readonly IMediator _mediator;

    public AccountController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Deletes all of the user's data (customers, invoices, worked days, templates, settings, e-invoicing
    /// records...) and destroys their encryption key. It can't be undone: the body must confirm it with
    /// <c>{ "confirmation": "DELETE" }</c>.
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(typeof(DeleteAccountResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeleteAccountResultDto>> Delete(
        [FromBody] DeleteAccountRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new DeleteAccountCommand(request?.Confirmation), cancellationToken));
}
