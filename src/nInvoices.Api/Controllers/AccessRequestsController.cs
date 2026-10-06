using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Account;

namespace nInvoices.Api.Controllers;

/// <summary>
/// Called by the "waiting for approval" page: tells the administrator, once per account, that someone
/// signed in and needs the "user" role. Open to any signed-in account, approved or not.
/// </summary>
[ApiController]
[Route("api/access-requests")]
[Authorize(Policy = SignedInPolicy)]
public sealed class AccessRequestsController : ControllerBase
{
    public const string SignedInPolicy = "SignedIn";

    private readonly IMediator _mediator;

    public AccessRequestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Records the request and notifies the administrator if they haven't been told yet. Approved
    /// users have nothing to request.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AccessRequestResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccessRequestResultDto>> Submit(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new RequestAccessCommand(User.FindFirst("name")?.Value), cancellationToken));
}
