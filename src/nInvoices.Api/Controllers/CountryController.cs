using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Country;

namespace nInvoices.Api.Controllers;

/// <summary>The country the user works from: only its invoicing rules and tax regimes apply.</summary>
[ApiController]
[Route("api/country")]
[Authorize]
public sealed class CountryController : ControllerBase
{
    private readonly IMediator _mediator;

    public CountryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The user's country (chosen, or inferred from what they use) and the countries offered.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserCountryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserCountryDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetUserCountryQuery(), cancellationToken));
    }

    /// <summary>Sets the user's country; the invoicing rules of the others are turned off, their settings kept.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(UserCountryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserCountryDto>> Set([FromBody] SetUserCountryDto dto, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new SetUserCountryCommand(dto), cancellationToken));
    }
}
