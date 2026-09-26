using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Taxes.Commands;
using nInvoices.Application.Features.Taxes.Queries;

namespace nInvoices.Api.Controllers;

/// <summary>
/// API controller for tax configuration management.
/// Manages tax rules for invoicing including percentage, fixed, and compound taxes.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class TaxesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<TaxesController> _logger;

    public TaxesController(IMediator mediator, ILogger<TaxesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Gets all tax configurations.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TaxDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TaxDto>>> GetAll(CancellationToken cancellationToken)
    {
        var query = new GetAllTaxesQuery();
        var taxes = await _mediator.Send(query, cancellationToken);
        return Ok(taxes);
    }

    /// <summary>
    /// Gets a tax configuration by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TaxDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaxDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var query = new GetTaxByIdQuery(id);
        var tax = await _mediator.Send(query, cancellationToken);

        if (tax == null)
        {
            _logger.LogWarning("Tax with ID {TaxId} not found", id);
            return NotFound();
        }

        return Ok(tax);
    }

    /// <summary>
    /// Gets all tax configurations for a specific customer.
    /// Used during invoice generation to apply customer-specific taxes.
    /// </summary>
    [HttpGet("customer/{customerId}")]
    [ProducesResponseType(typeof(IEnumerable<TaxDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TaxDto>>> GetByCustomerId(
        long customerId, 
        CancellationToken cancellationToken)
    {
        var query = new GetTaxesByCustomerIdQuery(customerId);
        var taxes = await _mediator.Send(query, cancellationToken);
        return Ok(taxes);
    }

    /// <summary>
    /// Creates a new tax configuration.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TaxDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaxDto>> Create(
        [FromBody] CreateTaxDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new CreateTaxCommand(dto);
            var tax = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation(
                "Created tax configuration {TaxId} for customer {CustomerId} with handler {HandlerId}", 
                tax.TaxId, tax.CustomerId, tax.HandlerId);
            return CreatedAtAction(nameof(GetById), new { id = tax.Id }, tax);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Customer not found when creating tax");
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing tax configuration.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TaxDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaxDto>> Update(
        long id,
        [FromBody] UpdateTaxDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UpdateTaxCommand(id, dto);
            var tax = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation("Updated tax configuration with ID {TaxId}", id);
            return Ok(tax);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Tax with ID {TaxId} not found for update", id);
            return NotFound();
        }
    }

    /// <summary>
    /// Deletes a tax configuration.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var command = new DeleteTaxCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result)
        {
            _logger.LogWarning("Tax with ID {TaxId} not found for deletion", id);
            return NotFound();
        }

        _logger.LogInformation("Deleted tax configuration with ID {TaxId}", id);
        return NoContent();
    }
}