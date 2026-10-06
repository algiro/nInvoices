using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Invoices.Commands;
using nInvoices.Application.Features.Invoices.Queries;
using nInvoices.Application.Models;
using nInvoices.Application.Features.EInvoices;
using nInvoices.Application.Features.EInvoices.Commands;
using nInvoices.Application.Features.EInvoices.Queries;
using nInvoices.Application.Features.InvoiceEmails.Commands;
using nInvoices.Application.Features.InvoiceEmails.Queries;

namespace nInvoices.Api.Controllers;

/// <summary>
/// Manages invoice generation, retrieval, and lifecycle operations.
/// Implements RESTful API endpoints following CQRS pattern.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(
        IMediator mediator, 
        ILogger<InvoicesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all invoices.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<InvoiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<InvoiceDto>>> GetAll(CancellationToken cancellationToken)
    {
        var query = new GetAllInvoicesQuery();
        var invoices = await _mediator.Send(query, cancellationToken);
        return Ok(invoices);
    }

    /// <summary>
    /// One page of invoices, filtered and sorted in the database, with the number of matching
    /// invoices per status. Invoices in the page don't carry their rendered HTML.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(InvoicePageDto), StatusCodes.Status200OK)]
    // Not named "search": a parameter named like one of its own query keys makes MVC bind with
    // that name as a prefix ("search.status"…) and every filter would be ignored
    public async Task<ActionResult<InvoicePageDto>> Search([FromQuery] InvoiceSearchDto request, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new SearchInvoicesQuery(request), cancellationToken));

    /// <summary>Outstanding and paid-this-year totals, drafts and invoice years, over all invoices.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(InvoiceSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceSummaryDto>> Summary(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetInvoiceSummaryQuery(DateTime.Today.Year), cancellationToken));

    /// <summary>
    /// Finalizes, marks as sent or marks as paid several invoices. Invoices the change doesn't
    /// apply to are skipped and listed with the reason.
    /// </summary>
    /// <param name="change"><c>finalize</c>, <c>mark-as-sent</c> or <c>mark-as-paid</c>.</param>
    [HttpPost("bulk/{change:regex(^(finalize|mark-as-sent|mark-as-paid)$)}")]
    [ProducesResponseType(typeof(BulkInvoiceResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkInvoiceResultDto>> BulkChangeStatus(
        string change,
        [FromBody] BulkInvoiceIdsDto dto,
        CancellationToken cancellationToken)
    {
        var bulkAction = change switch
        {
            "finalize" => BulkInvoiceStatusAction.Finalize,
            "mark-as-sent" => BulkInvoiceStatusAction.MarkAsSent,
            _ => BulkInvoiceStatusAction.MarkAsPaid
        };

        var result = await _mediator.Send(new BulkChangeInvoiceStatusCommand(bulkAction, dto.Ids ?? []), cancellationToken);
        _logger.LogInformation(
            "Bulk {Action}: {Succeeded} changed, {Skipped} skipped",
            bulkAction, result.Succeeded.Count, result.Skipped.Count);
        return Ok(result);
    }

    /// <summary>
    /// The PDFs of several invoices in one zip, optionally with the timesheets of the monthly ones.
    /// Invoices whose documents can't be produced are listed in NOT-INCLUDED.txt inside the zip.
    /// </summary>
    [HttpPost("bulk/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> BulkDownload([FromBody] BulkInvoiceDownloadDto dto, CancellationToken cancellationToken)
    {
        var zip = await _mediator.Send(
            new GetInvoiceDocumentsZipQuery(dto.Ids ?? [], dto.IncludeMonthlyReports), cancellationToken);
        return Download(zip);
    }

    /// <summary>
    /// Retrieves an invoice by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var query = new GetInvoiceByIdQuery(id);
        var invoice = await _mediator.Send(query, cancellationToken);

        if (invoice == null)
            return NotFound();

        return Ok(invoice);
    }

    /// <summary>
    /// Retrieves all invoices for a specific customer.
    /// </summary>
    [HttpGet("customer/{customerId}")]
    [ProducesResponseType(typeof(IEnumerable<InvoiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<InvoiceDto>>> GetByCustomer(
        long customerId,
        CancellationToken cancellationToken)
    {
        var query = new GetInvoicesByCustomerQuery(customerId);
        var invoices = await _mediator.Send(query, cancellationToken);
        return Ok(invoices);
    }

    /// <summary>
    /// Retrieves invoices for a specific customer and period.
    /// </summary>
    [HttpGet("customer/{customerId}/period")]
    [ProducesResponseType(typeof(IEnumerable<InvoiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<InvoiceDto>>> GetByPeriod(
        long customerId,
        [FromQuery] int year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        var query = new GetInvoicesByPeriodQuery(customerId, year, month);
        var invoices = await _mediator.Send(query, cancellationToken);
        return Ok(invoices);
    }

    /// <summary>
    /// Generates a new invoice based on provided data.
    /// Business logic orchestration via InvoiceGenerationService.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvoiceDto>> Generate(
        [FromBody] GenerateInvoiceDto dto,
        CancellationToken cancellationToken)
    {
        var command = new GenerateInvoiceCommand(dto);
        var invoice = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = invoice.Id },
            invoice);
    }

    /// <summary>
    /// Renders the invoice and timesheet that generating <paramref name="dto"/> would produce,
    /// without saving anything. Problems are returned in <c>errors</c> / <c>timesheetError</c>.
    /// </summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(InvoiceDraftPreviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceDraftPreviewDto>> Preview(
        [FromBody] GenerateInvoiceDto dto,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new PreviewInvoiceDraftQuery(dto), cancellationToken));

    /// <summary>
    /// Updates a draft invoice (notes, rendered content, due date).
    /// Only draft invoices can be updated.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(
        long id,
        [FromBody] UpdateInvoiceDto dto,
        CancellationToken cancellationToken)
    {
        var command = new UpdateInvoiceCommand(id, dto);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Finalizes a draft invoice, making it immutable.
    /// Transitions status from Draft to Finalized.
    /// </summary>
    [HttpPost("{id}/finalize")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Finalize(long id, CancellationToken cancellationToken)
    {
        var command = new FinalizeInvoiceCommand(id);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Marks a finalized invoice as sent.
    /// Transitions status from Finalized to Sent.
    /// </summary>
    [HttpPost("{id}/mark-as-sent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> MarkAsSent(long id, CancellationToken cancellationToken)
    {
        var command = new MarkAsSentCommand(id);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Marks a sent invoice as paid.
    /// Transitions status from Sent to Paid.
    /// </summary>
    [HttpPost("{id}/mark-as-paid")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> MarkAsPaid(long id, CancellationToken cancellationToken)
    {
        var command = new MarkAsPaidCommand(id);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Cancels an invoice.
    /// Transitions status to Cancelled.
    /// </summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        var command = new CancelInvoiceCommand(id);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes an invoice.
    /// By default, only draft invoices can be deleted.
    /// Use force=true query parameter to delete finalized invoices (e.g., when there was a generation error).
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(
        long id,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteInvoiceCommand(id, force);
        await _mediator.Send(command, cancellationToken);
        
        if (force)
            _logger.LogWarning("Invoice {InvoiceId} was force deleted", id);
        
        return NoContent();
    }
    /// <summary>
    /// The structured e-invoice formats (Facturae...) that apply to the invoice, with the generated file
    /// if any. Empty unless the user turned on a country that has such a format.
    /// </summary>
    [HttpGet("{id}/einvoices")]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceEInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<InvoiceEInvoiceDto>>> GetEInvoices(long id, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetInvoiceEInvoicesQuery(id), cancellationToken));
    }

    /// <summary>
    /// Generates the invoice in each e-invoice format that applies, replacing the stored file. A format
    /// the invoice does not meet comes back with its <c>issues</c>; the others are generated.
    /// </summary>
    [HttpPost("{id}/einvoices")]
    [ProducesResponseType(typeof(IReadOnlyList<EInvoiceGenerationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EInvoiceGenerationDto>>> GenerateEInvoices(long id, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GenerateInvoiceEInvoicesCommand(id), cancellationToken));
    }

    /// <summary>Downloads the generated e-invoice file (e.g. the signed Facturae XML) of an invoice.</summary>
    [HttpGet("{id}/einvoices/{formatId}/file")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DownloadEInvoice(long id, string formatId, CancellationToken cancellationToken)
    {
        var file = await _mediator.Send(new GetInvoiceEInvoiceFileQuery(id, formatId), cancellationToken);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>
    /// The channels (FACe...) the e-invoice of this invoice can be delivered through, with what is missing
    /// to use them and the delivery if one was made. Empty unless the invoice goes through one.
    /// </summary>
    [HttpGet("{id}/einvoice-channels")]
    [ProducesResponseType(typeof(IReadOnlyList<EInvoiceChannelDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EInvoiceChannelDto>>> GetEInvoiceChannels(long id, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetInvoiceChannelsQuery(id), cancellationToken));
    }

    /// <summary>
    /// Sends the e-invoice through the channel (e.g. face). It reaches a public body and cannot be taken back,
    /// so it is only ever done on request. A refusal comes back as 400 with the reason; nothing is stored then.
    /// </summary>
    [HttpPost("{id}/einvoice-channels/{channelId}/send")]
    [ProducesResponseType(typeof(EInvoiceDeliveryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EInvoiceDeliveryDto>> SendEInvoice(long id, string channelId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SendInvoiceEInvoiceCommand(id, channelId), cancellationToken);
        if (!result.Succeeded)
            _logger.LogWarning("Sending invoice {InvoiceId} through {Channel} failed: {Message}", id, channelId, result.Message);
        return result.Succeeded ? Ok(result) : BadRequest(new { error = result.Message, submission = result.Submission });
    }

    /// <summary>Asks the channel where the sent e-invoice stands now and stores the answer.</summary>
    [HttpPost("{id}/einvoice-channels/{channelId}/refresh")]
    [ProducesResponseType(typeof(EInvoiceDeliveryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EInvoiceDeliveryDto>> RefreshEInvoice(long id, string channelId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RefreshInvoiceEInvoiceCommand(id, channelId), cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(new { error = result.Message, submission = result.Submission });
    }

    /// <summary>The invoice as a PDF, for download.</summary>
    [HttpGet("{id}/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ExportPdf(long id, CancellationToken cancellationToken) =>
        Download(await _mediator.Send(new GetInvoicePdfQuery(id), cancellationToken));

    /// <summary>The calendar of worked days of a monthly invoice as a PDF, for timesheet documentation.</summary>
    [HttpGet("{id}/calendar/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ExportCalendarPdf(long id, CancellationToken cancellationToken) =>
        Download(await _mediator.Send(new GetWorkedDaysCalendarPdfQuery(id), cancellationToken));

    /// <summary>
    /// The monthly report of a monthly invoice as a PDF, rendered with its template: worked days,
    /// holidays and unpaid leave of the month.
    /// </summary>
    [HttpGet("{id}/monthlyreport/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ExportMonthlyReportPdf(long id, CancellationToken cancellationToken) =>
        Download(await _mediator.Send(new GetMonthlyReportPdfQuery(id), cancellationToken));

    /// <summary>
    /// Renders the invoice again with the customer's current active template, e.g. after the
    /// template was changed. The amounts don't change.
    /// </summary>
    [HttpPost("{id}/regenerate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RegenerateInvoicePdf(long id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new RegenerateInvoiceHtmlCommand(id), cancellationToken);
        return Ok(new { message = "Invoice PDF regenerated successfully. Download the invoice to see the updated version." });
    }

    /// <summary>
    /// Checks that the monthly report renders with the current template. Reports are rendered on
    /// each download, so there is nothing stored to regenerate.
    /// </summary>
    [HttpPost("{id}/monthlyreport/regenerate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RegenerateMonthlyReportPdf(long id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new VerifyMonthlyReportQuery(id), cancellationToken);
        return Ok(new { message = "Monthly report is ready to be generated with current template" });
    }

    /// <summary>
    /// Gets how the current user's invoices are numbered: the sequence, the pattern and the next number.
    /// </summary>
    [HttpGet("numbering")]
    [ProducesResponseType(typeof(InvoiceNumberingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceNumberingDto>> GetNumbering(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetInvoiceNumberingQuery(), cancellationToken));
    }

    /// <summary>
    /// Sets the current user's next sequence value and pattern (blank pattern = the default one).
    /// WARNING: Setting the value too low can cause duplicate invoice numbers.
    /// </summary>
    [HttpPut("numbering")]
    [ProducesResponseType(typeof(InvoiceNumberingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvoiceNumberingDto>> UpdateNumbering(
        [FromBody] UpdateInvoiceNumberingDto dto,
        CancellationToken cancellationToken)
    {
        var numbering = await _mediator.Send(new UpdateInvoiceNumberingCommand(dto), cancellationToken);

        _logger.LogInformation("Updated invoice numbering: next value {Value}", numbering.CurrentValue);
        return Ok(numbering);
    }

    /// <summary>
    /// Prepares the email for an invoice from the customer's active email template (or the one
    /// given), for review. Rendering problems are returned in <c>errors</c>.
    /// </summary>
    [HttpGet("{id}/email")]
    [ProducesResponseType(typeof(InvoiceEmailComposeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceEmailComposeDto>> ComposeEmail(
        long id,
        [FromQuery] long? templateId,
        CancellationToken cancellationToken)
    {
        var compose = await _mediator.Send(new GetInvoiceEmailComposeQuery(id, templateId), cancellationToken);
        return compose is null ? NotFound() : Ok(compose);
    }

    /// <summary>
    /// Creates a draft with the reviewed email and the invoice documents in the user's Gmail.
    /// The user reviews and sends it from Gmail; the invoice status does not change.
    /// </summary>
    [HttpPost("{id}/email/draft")]
    [ProducesResponseType(typeof(InvoiceEmailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceEmailDto>> CreateEmailDraft(
        long id,
        [FromBody] CreateInvoiceEmailDraftDto dto,
        CancellationToken cancellationToken)
    {
        var email = await _mediator.Send(new CreateInvoiceEmailDraftCommand(id, dto), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, email);
    }

    /// <summary>The Gmail drafts created for an invoice, newest first.</summary>
    [HttpGet("{id}/emails")]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceEmailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InvoiceEmailDto>>> GetEmails(long id, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetInvoiceEmailsQuery(id), cancellationToken));

    private FileContentResult Download(DownloadFile file) => File(file.Content, file.ContentType, file.FileName);
}
