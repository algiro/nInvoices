using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Customers.Commands;
using nInvoices.Application.Features.Customers.Queries;
using MediatR;

namespace nInvoices.Api.Controllers;

/// <summary>
/// API controller for customer management.
/// Follows RESTful principles and delegates to MediatR handlers.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CustomersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(IMediator mediator, ILogger<CustomersController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Gets all customers.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CustomerDto>>> GetAll(CancellationToken cancellationToken)
    {
        var query = new GetAllCustomersQuery();
        var customers = await _mediator.Send(query, cancellationToken);
        return Ok(customers);
    }

    /// <summary>
    /// Gets a customer by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var query = new GetCustomerByIdQuery(id);
        var customer = await _mediator.Send(query, cancellationToken);

        if (customer == null)
        {
            _logger.LogWarning("Customer with ID {CustomerId} not found", id);
            return NotFound();
        }

        return Ok(customer);
    }

    /// <summary>
    /// Creates a new customer.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerDto>> Create(
        [FromBody] CreateCustomerDto dto,
        CancellationToken cancellationToken)
    {
        var command = new CreateCustomerCommand(dto);
        var customer = await _mediator.Send(command, cancellationToken);

        _logger.LogInformation("Created customer with ID {CustomerId}", customer.Id);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    /// <summary>
    /// Updates an existing customer.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerDto>> Update(
        long id,
        [FromBody] UpdateCustomerDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UpdateCustomerCommand(id, dto);
            var customer = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation("Updated customer with ID {CustomerId}", id);
            return Ok(customer);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Customer with ID {CustomerId} not found for update", id);
            return NotFound();
        }
    }

    /// <summary>
    /// Deletes a customer.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var command = new DeleteCustomerCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result)
        {
            _logger.LogWarning("Customer with ID {CustomerId} not found for deletion", id);
            return NotFound();
        }

        _logger.LogInformation("Deleted customer with ID {CustomerId}", id);
        return NoContent();
    }
}
