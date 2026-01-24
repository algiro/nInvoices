using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Api.Controllers;

/// <summary>
/// API controller for managing monthly report templates.
/// Handles CRUD operations and template validation.
/// </summary>
[ApiController]
[Route("api/monthlyreporttemplates")]
public sealed class MonthlyReportTemplatesController : ControllerBase
{
    private readonly IRepository<MonthlyReportTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly ILogger<MonthlyReportTemplatesController> _logger;

    public MonthlyReportTemplatesController(
        IRepository<MonthlyReportTemplate> repository,
        IUnitOfWork unitOfWork,
        ITemplateRenderer templateRenderer,
        ILogger<MonthlyReportTemplatesController> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _templateRenderer = templateRenderer;
        _logger = logger;
    }

    /// <summary>
    /// Gets all monthly report templates for a specific customer.
    /// </summary>
    [HttpGet("customer/{customerId}")]
    [ProducesResponseType(typeof(IEnumerable<MonthlyReportTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCustomer(long customerId, CancellationToken cancellationToken)
    {
        var templates = await _repository.FindAsync(
            t => t.CustomerId == customerId,
            cancellationToken);

        var dtos = templates.Select(t => new MonthlyReportTemplateDto(
            t.Id,
            t.CustomerId,
            t.InvoiceType,
            t.Name,
            t.Content,
            t.IsActive,
            t.CreatedAt,
            t.UpdatedAt));

        return Ok(dtos);
    }

    /// <summary>
    /// Gets a specific monthly report template by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MonthlyReportTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken);
        if (template == null)
            return NotFound();

        var dto = new MonthlyReportTemplateDto(
            template.Id,
            template.CustomerId,
            template.InvoiceType,
            template.Name,
            template.Content,
            template.IsActive,
            template.CreatedAt,
            template.UpdatedAt);

        return Ok(dto);
    }

    /// <summary>
    /// Creates a new monthly report template.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MonthlyReportTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMonthlyReportTemplateDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var template = new MonthlyReportTemplate(
                dto.CustomerId,
                dto.Name,
                dto.Content,
                dto.InvoiceType);

            await _repository.AddAsync(template, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Monthly report template {TemplateId} created for customer {CustomerId}",
                template.Id,
                template.CustomerId);

            var resultDto = new MonthlyReportTemplateDto(
                template.Id,
                template.CustomerId,
                template.InvoiceType,
                template.Name,
                template.Content,
                template.IsActive,
                template.CreatedAt,
                template.UpdatedAt);

            return CreatedAtAction(
                nameof(GetById),
                new { id = template.Id },
                resultDto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing monthly report template.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(MonthlyReportTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpdateMonthlyReportTemplateDto dto,
        CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken);
        if (template == null)
            return NotFound();

        try
        {
            template.Update(dto.Name, dto.Content);
            await _repository.UpdateAsync(template, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Monthly report template {TemplateId} updated",
                template.Id);

            var resultDto = new MonthlyReportTemplateDto(
                template.Id,
                template.CustomerId,
                template.InvoiceType,
                template.Name,
                template.Content,
                template.IsActive,
                template.CreatedAt,
                template.UpdatedAt);

            return Ok(resultDto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a monthly report template.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken);
        if (template == null)
            return NotFound();

        await _repository.DeleteAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Monthly report template {TemplateId} deleted",
            id);

        return NoContent();
    }

    /// <summary>
    /// Validates monthly report template syntax.
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(TemplateValidationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Validate(
        [FromBody] string content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new { errors = new[] { "Template content cannot be empty" } });

        var result = await _templateRenderer.ValidateAsync(content, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Activates a monthly report template.
    /// </summary>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(long id, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken);
        if (template == null)
            return NotFound();

        template.Activate();
        await _repository.UpdateAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Monthly report template {TemplateId} activated", id);

        return Ok();
    }

    /// <summary>
    /// Deactivates a monthly report template.
    /// </summary>
    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(long id, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken);
        if (template == null)
            return NotFound();

        template.Deactivate();
        await _repository.UpdateAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Monthly report template {TemplateId} deactivated", id);

        return Ok();
    }
}
