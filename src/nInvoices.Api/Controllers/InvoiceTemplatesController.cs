using MediatR;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.InvoiceTemplates.Commands;
using nInvoices.Application.Features.InvoiceTemplates.Queries;
using nInvoices.Core.Enums;

namespace nInvoices.Api.Controllers;

/// <summary>
/// API controller for invoice template management.
/// Templates define the structure and content of generated invoices.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class InvoiceTemplatesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<InvoiceTemplatesController> _logger;

    public InvoiceTemplatesController(IMediator mediator, ILogger<InvoiceTemplatesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Gets all invoice templates.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<InvoiceTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<InvoiceTemplateDto>>> GetAll(CancellationToken cancellationToken)
    {
        var query = new GetAllInvoiceTemplatesQuery();
        var templates = await _mediator.Send(query, cancellationToken);
        return Ok(templates);
    }

    /// <summary>
    /// Gets an invoice template by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(InvoiceTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceTemplateDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var query = new GetInvoiceTemplateByIdQuery(id);
        var template = await _mediator.Send(query, cancellationToken);

        if (template == null)
        {
            _logger.LogWarning("Template with ID {TemplateId} not found", id);
            return NotFound();
        }

        return Ok(template);
    }

    /// <summary>
    /// Gets all templates for a specific customer.
    /// </summary>
    [HttpGet("customer/{customerId}")]
    [ProducesResponseType(typeof(IEnumerable<InvoiceTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<InvoiceTemplateDto>>> GetByCustomerId(
        long customerId, 
        CancellationToken cancellationToken)
    {
        var query = new GetTemplatesByCustomerIdQuery(customerId);
        var templates = await _mediator.Send(query, cancellationToken);
        return Ok(templates);
    }

    /// <summary>
    /// Gets template for specific customer and invoice type.
    /// Used during invoice generation to find the correct template.
    /// </summary>
    [HttpGet("customer/{customerId}/type/{type}")]
    [ProducesResponseType(typeof(InvoiceTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceTemplateDto>> GetByCustomerAndType(
        long customerId,
        InvoiceType type,
        CancellationToken cancellationToken)
    {
        var query = new GetTemplateByCustomerAndTypeQuery(customerId, type);
        var template = await _mediator.Send(query, cancellationToken);

        if (template == null)
        {
            _logger.LogWarning(
                "Template not found for customer {CustomerId} and type {Type}", 
                customerId, type);
            return NotFound();
        }

        return Ok(template);
    }

    /// <summary>
    /// Validates template syntax without saving.
    /// Returns validation errors and extracted placeholders.
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(TemplateValidationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TemplateValidationResultDto>> ValidateTemplate(
        [FromBody] string content,
        CancellationToken cancellationToken)
    {
        var command = new ValidateTemplateCommand(content);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new invoice template.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(InvoiceTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceTemplateDto>> Create(
        [FromBody] CreateInvoiceTemplateDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new CreateInvoiceTemplateCommand(dto);
            var template = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation(
                "Created template with ID {TemplateId} for customer {CustomerId} and type {InvoiceType}", 
                template.Id, template.CustomerId, template.InvoiceType);
            return CreatedAtAction(nameof(GetById), new { id = template.Id }, template);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Customer not found when creating template");
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid template syntax");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing invoice template.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(InvoiceTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvoiceTemplateDto>> Update(
        long id,
        [FromBody] UpdateInvoiceTemplateDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UpdateInvoiceTemplateCommand(id, dto);
            var template = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation("Updated template with ID {TemplateId}", id);
            return Ok(template);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Template with ID {TemplateId} not found for update", id);
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid template syntax");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deletes an invoice template.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var command = new DeleteInvoiceTemplateCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result)
        {
            _logger.LogWarning("Template with ID {TemplateId} not found for deletion", id);
            return NotFound();
        }

        _logger.LogInformation("Deleted template with ID {TemplateId}", id);
        return NoContent();
    }
}