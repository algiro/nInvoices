using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.EmailTemplates.Commands;
using nInvoices.Application.Features.EmailTemplates.Queries;
using nInvoices.Application.Mappings;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Api.Controllers;

/// <summary>
/// Per-customer templates for the subject and body of invoice emails.
/// At most one template per customer is active; it is preselected when composing an email.
/// </summary>
[ApiController]
[Route("api/emailtemplates")]
[Authorize]
public sealed class EmailTemplatesController : ControllerBase
{
    private readonly IInvoiceEmailComposer _composer;
    private readonly IMediator _mediator;

    public EmailTemplatesController(
        IInvoiceEmailComposer composer,
        IMediator mediator)
    {
        _composer = composer;
        _mediator = mediator;
    }

    /// <summary>The templates shared by all of the user's customers.</summary>
    [HttpGet("shared")]
    [ProducesResponseType(typeof(IEnumerable<EmailTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmailTemplateDto>>> GetShared(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetEmailTemplatesQuery(null), cancellationToken));
    }

    [HttpGet("customer/{customerId}")]
    [ProducesResponseType(typeof(IEnumerable<EmailTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmailTemplateDto>>> GetByCustomer(long customerId, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetEmailTemplatesQuery(customerId), cancellationToken));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EmailTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailTemplateDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var template = await _mediator.Send(new GetEmailTemplateByIdQuery(id), cancellationToken);
        return template is null ? NotFound() : Ok(template);
    }

    /// <summary>The built-in subject and body, as a starting point for a new template.</summary>
    [HttpGet("default")]
    [ProducesResponseType(typeof(UpdateEmailTemplateDto), StatusCodes.Status200OK)]
    public ActionResult<UpdateEmailTemplateDto> GetDefault() =>
        Ok(new UpdateEmailTemplateDto("Invoice email", DefaultEmailTemplate.Subject, DefaultEmailTemplate.Body));

    /// <summary>
    /// Creates a template (with no customer, one shared by all customers). The first email template
    /// of a customer, or the first shared one, becomes active automatically.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmailTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmailTemplateDto>> Create([FromBody] CreateEmailTemplateDto dto, CancellationToken cancellationToken)
    {
        var template = await _mediator.Send(new CreateEmailTemplateCommand(dto), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = template.Id }, template);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(EmailTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmailTemplateDto>> Update(long id, [FromBody] UpdateEmailTemplateDto dto, CancellationToken cancellationToken)
    {
        var template = await _mediator.Send(new UpdateEmailTemplateCommand(id, dto), cancellationToken);
        return template is null ? NotFound() : Ok(template);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        return await _mediator.Send(new DeleteEmailTemplateCommand(id), cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>Makes this the customer's active email template, deactivating the others.</summary>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(long id, CancellationToken cancellationToken)
    {
        return await _mediator.Send(new ActivateEmailTemplateCommand(id), cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(long id, CancellationToken cancellationToken)
    {
        return await _mediator.Send(new DeactivateEmailTemplateCommand(id), cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Renders a subject and body with the customer's latest invoice (or sample figures),
    /// without saving. Errors are returned in the result, not as a failed request.
    /// </summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(EmailTemplatePreviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmailTemplatePreviewDto>> Preview([FromBody] PreviewEmailTemplateDto dto, CancellationToken cancellationToken)
    {
        var result = await _composer.PreviewAsync(dto.Subject ?? string.Empty, dto.Body ?? string.Empty, dto.CustomerId, cancellationToken);
        return Ok(new EmailTemplatePreviewDto(result.Subject, result.Html, result.Errors));
    }
}
