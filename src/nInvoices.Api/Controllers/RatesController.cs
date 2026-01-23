using MediatR;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Rates.Commands;
using nInvoices.Application.Features.Rates.Queries;

namespace nInvoices.Api.Controllers;

/// <summary>
/// API controller for rate management.
/// Manages pricing information for customers.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class RatesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<RatesController> _logger;

    public RatesController(IMediator mediator, ILogger<RatesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Gets all rates.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RateDto>>> GetAll(CancellationToken cancellationToken)
    {
        var query = new GetAllRatesQuery();
        var rates = await _mediator.Send(query, cancellationToken);
        return Ok(rates);
    }

    /// <summary>
    /// Gets a rate by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RateDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var query = new GetRateByIdQuery(id);
        var rate = await _mediator.Send(query, cancellationToken);

        if (rate == null)
        {
            _logger.LogWarning("Rate with ID {RateId} not found", id);
            return NotFound();
        }

        return Ok(rate);
    }

    /// <summary>
    /// Gets all rates for a specific customer.
    /// </summary>
    [HttpGet("customer/{customerId}")]
    [ProducesResponseType(typeof(IEnumerable<RateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RateDto>>> GetByCustomerId(
        long customerId, 
        CancellationToken cancellationToken)
    {
        var query = new GetRatesByCustomerIdQuery(customerId);
        var rates = await _mediator.Send(query, cancellationToken);
        return Ok(rates);
    }

    /// <summary>
    /// Creates a new rate.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RateDto>> Create(
        [FromBody] CreateRateDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new CreateRateCommand(dto);
            var rate = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation("Created rate with ID {RateId} for customer {CustomerId}", 
                rate.Id, rate.CustomerId);
            return CreatedAtAction(nameof(GetById), new { id = rate.Id }, rate);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Customer not found when creating rate");
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing rate.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(RateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RateDto>> Update(
        long id,
        [FromBody] UpdateRateDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UpdateRateCommand(id, dto);
            var rate = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation("Updated rate with ID {RateId}", id);
            return Ok(rate);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Rate with ID {RateId} not found for update", id);
            return NotFound();
        }
    }

    /// <summary>
    /// Deletes a rate.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var command = new DeleteRateCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result)
        {
            _logger.LogWarning("Rate with ID {RateId} not found for deletion", id);
            return NotFound();
        }

        _logger.LogInformation("Deleted rate with ID {RateId}", id);
        return NoContent();
    }
}