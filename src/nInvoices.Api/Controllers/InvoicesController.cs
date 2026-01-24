using MediatR;
using nInvoices.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Invoices.Commands;
using nInvoices.Application.Features.Invoices.Queries;

namespace nInvoices.Api.Controllers;

/// <summary>
/// Manages invoice generation, retrieval, and lifecycle operations.
/// Implements RESTful API endpoints following CQRS pattern.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<InvoicesController> _logger;
    private readonly IPdfExportService _pdfExportService;

    public InvoicesController(
        IMediator mediator, 
        ILogger<InvoicesController> logger,
        IPdfExportService pdfExportService)
    {
        _mediator = mediator;
        _logger = logger;
        _pdfExportService = pdfExportService;
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
        try
        {
            var command = new GenerateInvoiceCommand(dto);
            var invoice = await _mediator.Send(command, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = invoice.Id },
                invoice);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to generate invoice");
            return BadRequest(new { error = ex.Message });
        }
    }

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
        try
        {
            var command = new UpdateInvoiceCommand(id, dto);
            await _mediator.Send(command, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to update invoice {InvoiceId}", id);
            return BadRequest(new { error = ex.Message });
        }
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
        try
        {
            var command = new FinalizeInvoiceCommand(id);
            await _mediator.Send(command, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to finalize invoice {InvoiceId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a draft invoice.
    /// Only draft invoices can be deleted.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        try
        {
            var command = new DeleteInvoiceCommand(id);
            await _mediator.Send(command, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to delete invoice {InvoiceId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }
    /// <summary>
    /// Exports an invoice as PDF.
    /// Returns PDF file for download.
    /// </summary>
    [HttpGet("{id}/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ExportPdf(long id, CancellationToken cancellationToken)
    {
        var query = new GetInvoiceByIdQuery(id);
        var invoiceDto = await _mediator.Send(query, cancellationToken);

        if (invoiceDto == null)
            return NotFound();

        var repository = HttpContext.RequestServices.GetRequiredService<IRepository<Core.Entities.Invoice>>();
        var invoice = await repository.GetByIdAsync(id, cancellationToken);

        if (invoice == null)
            return NotFound();

        try
        {
            var pdfBytes = _pdfExportService.GenerateInvoicePdf(invoice);
            var fileName = $"Invoice-{invoice.Number}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF for invoice {InvoiceId}", id);
            return StatusCode(500, new { error = "Failed to generate PDF" });
        }
    }

    /// <summary>
    /// Exports worked days calendar as PDF for a monthly invoice.
    /// Useful for timesheet documentation.
    /// </summary>
    [HttpGet("{id}/calendar/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ExportCalendarPdf(long id, CancellationToken cancellationToken)
    {
        var repository = HttpContext.RequestServices.GetRequiredService<IRepository<Core.Entities.Invoice>>();
        var invoice = await repository.GetByIdAsync(id, cancellationToken);

        if (invoice == null)
            return NotFound();

        if (invoice.Type != Core.Enums.InvoiceType.Monthly)
            return BadRequest(new { error = "Calendar export is only available for monthly invoices" });

        try
        {
            var pdfBytes = _pdfExportService.GenerateWorkedDaysCalendarPdf(invoice);
            var fileName = $"Calendar-{invoice.Year}-{invoice.Month:00}-{invoice.Customer?.Name}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate calendar PDF for invoice {InvoiceId}", id);
            return StatusCode(500, new { error = "Failed to generate calendar PDF" });
        }
    }

    /// <summary>
    /// Exports monthly report as PDF using custom template.
    /// Shows worked days, holidays, and unpaid leave for the month.
    /// </summary>
    [HttpGet("{id}/monthlyreport/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ExportMonthlyReportPdf(long id, CancellationToken cancellationToken)
    {
        var repository = HttpContext.RequestServices.GetRequiredService<IRepository<Core.Entities.Invoice>>();
        var customerRepository = HttpContext.RequestServices.GetRequiredService<IRepository<Core.Entities.Customer>>();
        var monthlyReportService = HttpContext.RequestServices.GetRequiredService<Application.Services.IMonthlyReportGenerationService>();
        var htmlToPdfConverter = HttpContext.RequestServices.GetRequiredService<Application.Services.IHtmlToPdfConverter>();

        var invoice = await repository.GetByIdAsync(id, cancellationToken);
        if (invoice == null)
            return NotFound();

        if (invoice.Type != Core.Enums.InvoiceType.Monthly)
            return BadRequest(new { error = "Monthly reports are only available for monthly invoices" });

        try
        {
            var customer = await customerRepository.GetByIdAsync(invoice.CustomerId, cancellationToken);
            if (customer == null)
                return NotFound(new { error = "Customer not found" });

            // Generate HTML from template
            var html = await monthlyReportService.GenerateReportHtmlAsync(invoice, customer, cancellationToken);

            // Convert to PDF
            var pdfBytes = await htmlToPdfConverter.ConvertAsync(html, cancellationToken);
            var fileName = $"MonthlyReport-{invoice.Year}-{invoice.Month:00}-{customer.Name}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Monthly report template not found for invoice {InvoiceId}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate monthly report PDF for invoice {InvoiceId}", id);
            return StatusCode(500, new { error = "Failed to generate monthly report PDF" });
        }
    }
}

