using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.TaxPrediction.Commands;
using nInvoices.Application.Features.TaxPrediction.Queries;

namespace nInvoices.Api.Controllers;

/// <summary>
/// The estimate of the user's taxes, period by period, from their invoices and business expenses;
/// where they pay taxes; and the payments they recorded.
/// </summary>
[ApiController]
[Route("api/tax-prediction")]
[Authorize]
public sealed class TaxPredictionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<TaxPredictionController> _logger;

    public TaxPredictionController(IMediator mediator, ILogger<TaxPredictionController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>The estimate of a year. Not set up (no periods) until the user says where they pay taxes.</summary>
    [HttpGet("{year:int}")]
    [ProducesResponseType(typeof(TaxPredictionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaxPredictionDto>> Get(int year, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetTaxPredictionQuery(year), cancellationToken));
    }

    /// <summary>The countries whose taxes can be estimated, with their regimes, and the user's profile.</summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(TaxPredictionSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaxPredictionSettingsDto>> GetSettings(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetTaxPredictionSettingsQuery(), cancellationToken));
    }

    /// <summary>Saves where the user pays taxes and how.</summary>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(TaxProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaxProfileDto>> UpdateProfile([FromBody] UpdateTaxProfileDto dto, CancellationToken cancellationToken)
    {
        var profile = await _mediator.Send(new UpdateTaxProfileCommand(dto), cancellationToken);
        _logger.LogInformation("Saved the tax profile ({Country}, {Regime})", profile.CountryCode, profile.RegimeId);
        return Ok(profile);
    }

    /// <summary>Records what was paid for a form and period, replacing an earlier record of the same.</summary>
    [HttpPut("payments")]
    [ProducesResponseType(typeof(TaxPaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaxPaymentDto>> RecordPayment([FromBody] RecordTaxPaymentDto dto, CancellationToken cancellationToken)
    {
        var payment = await _mediator.Send(new RecordTaxPaymentCommand(dto), cancellationToken);
        _logger.LogInformation("Recorded tax payment {PaymentId} ({Form} {Year}/{Period})", payment.Id, payment.Form, payment.Year, payment.Period);
        return Ok(payment);
    }

    /// <summary>Deletes a recorded payment: the estimate is used again for that period.</summary>
    [HttpDelete("payments/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePayment(long id, CancellationToken cancellationToken)
    {
        if (!await _mediator.Send(new DeleteTaxPaymentCommand(id), cancellationToken))
            return NotFound();

        _logger.LogInformation("Deleted tax payment {PaymentId}", id);
        return NoContent();
    }
}
