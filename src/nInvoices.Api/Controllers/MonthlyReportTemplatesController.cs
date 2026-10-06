using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.MonthlyReportTemplates.Commands;
using nInvoices.Application.Features.MonthlyReportTemplates.Queries;
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
    private readonly ITemplateRenderer _templateRenderer;
    private readonly ITemplatePreviewService _previewService;
    private readonly IMediator _mediator;

    public MonthlyReportTemplatesController(
        ITemplateRenderer templateRenderer,
        ITemplatePreviewService previewService,
        IMediator mediator)
    {
        _templateRenderer = templateRenderer;
        _previewService = previewService;
        _mediator = mediator;
    }

    /// <summary>
    /// Gets the monthly report templates shared by all of the user's customers.
    /// </summary>
    [HttpGet("shared")]
    [ProducesResponseType(typeof(IEnumerable<MonthlyReportTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShared(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetMonthlyReportTemplatesQuery(null), cancellationToken));
    }

    /// <summary>
    /// Gets all monthly report templates for a specific customer.
    /// </summary>
    [HttpGet("customer/{customerId}")]
    [ProducesResponseType(typeof(IEnumerable<MonthlyReportTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCustomer(long customerId, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetMonthlyReportTemplatesQuery(customerId), cancellationToken));
    }

    /// <summary>
    /// Gets a specific monthly report template by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MonthlyReportTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var template = await _mediator.Send(new GetMonthlyReportTemplateByIdQuery(id), cancellationToken);
        return template is null ? NotFound() : Ok(template);
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
        return await _mediator.Send(new DeleteMonthlyReportTemplateCommand(id), cancellationToken) ? NoContent() : NotFound();
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
        return await _mediator.Send(new ActivateMonthlyReportTemplateCommand(id), cancellationToken) ? Ok() : NotFound();
    }

    /// <summary>
    /// Deactivates a monthly report template.
    /// </summary>
    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(long id, CancellationToken cancellationToken)
    {
        return await _mediator.Send(new DeactivateMonthlyReportTemplateCommand(id), cancellationToken) ? Ok() : NotFound();
    }
}
