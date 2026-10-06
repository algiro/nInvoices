using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.MonthlyReportTemplates.Commands;
using nInvoices.Application.Mappings;
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
[Authorize]
public sealed class MonthlyReportTemplatesController : ControllerBase
{
    private readonly IRepository<MonthlyReportTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly ITemplatePreviewService _previewService;
    private readonly IMediator _mediator;
    private readonly ILogger<MonthlyReportTemplatesController> _logger;

    public MonthlyReportTemplatesController(
        IRepository<MonthlyReportTemplate> repository,
        IUnitOfWork unitOfWork,
        ITemplateRenderer templateRenderer,
        ITemplatePreviewService previewService,
        IMediator mediator,
        ILogger<MonthlyReportTemplatesController> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _templateRenderer = templateRenderer;
        _previewService = previewService;
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Gets the monthly report templates shared by all of the user's customers.
    /// </summary>
    [HttpGet("shared")]
    [ProducesResponseType(typeof(IEnumerable<MonthlyReportTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShared(CancellationToken cancellationToken)
    {
        var templates = await _repository.FindAsync(t => t.CustomerId == null, cancellationToken);

        return Ok(templates.Select(MonthlyReportTemplateMapper.ToDto));
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

        return Ok(templates.Select(MonthlyReportTemplateMapper.ToDto));
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

        return Ok(MonthlyReportTemplateMapper.ToDto(template));
    }

    /// <summary>
    /// Creates a new monthly report template. Content that doesn't parse is rejected with its syntax
    /// errors (400, by the validation pipeline).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MonthlyReportTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMonthlyReportTemplateDto dto,
        CancellationToken cancellationToken)
    {
        var template = await _mediator.Send(new CreateMonthlyReportTemplateCommand(dto), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = template.Id }, template);
    }

    /// <summary>
    /// Updates an existing monthly report template. Content that doesn't parse is rejected with its
    /// syntax errors (400, by the validation pipeline).
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
        var template = await _mediator.Send(new UpdateMonthlyReportTemplateCommand(id, dto), cancellationToken);
        return template is null ? NotFound() : Ok(template);
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
        [FromBody] ValidateTemplateDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest(new { errors = new[] { "Template content cannot be empty" } });

        var result = await _templateRenderer.ValidateAsync(dto.Content, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Renders template content with a sample month (optionally the given customer's details)
    /// without saving anything. Syntax and rendering problems come back as errors, not as a failure.
    /// </summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(TemplatePreviewResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(
        [FromBody] PreviewTemplateDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
            return Ok(new TemplatePreviewResult(null, ["Template content cannot be empty"]));

        return Ok(await _previewService.PreviewMonthlyReportAsync(dto.Content, dto.CustomerId, cancellationToken));
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

        // One active template per customer and type: the previous one is switched off
        var previouslyActive = await _repository.FindAsync(
            t => t.CustomerId == template.CustomerId && t.InvoiceType == template.InvoiceType && t.IsActive && t.Id != id,
            cancellationToken);
        foreach (var other in previouslyActive)
        {
            other.Deactivate();
            await _repository.UpdateAsync(other, cancellationToken);
        }

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
