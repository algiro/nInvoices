using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Preferences;

namespace nInvoices.Api.Controllers;

/// <summary>The user's preferences (the app's language): stored on the server, so they follow the user to every device.</summary>
[ApiController]
[Route("api/preferences")]
[Authorize]
public sealed class PreferencesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PreferencesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The user's preferences, and the languages the app is available in.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserPreferencesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserPreferencesDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetUserPreferencesQuery(), cancellationToken));
    }

    /// <summary>Changes the user's preferences.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(UserPreferencesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserPreferencesDto>> Update([FromBody] UpdateUserPreferencesDto dto, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new UpdateUserPreferencesCommand(dto), cancellationToken));
    }
}
