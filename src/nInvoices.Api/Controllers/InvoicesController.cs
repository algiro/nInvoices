using MediatR;
using nInvoices.Core.Interfaces;
using nInvoices.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Invoices.Commands;
using nInvoices.Application.Features.Invoices.Queries;
using nInvoices.Application.Models;

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
        try
        {
            var command = new DeleteInvoiceCommand(id, force);
            await _mediator.Send(command, cancellationToken);
            
            if (force)
                _logger.LogWarning("Invoice {InvoiceId} was force deleted", id);
            
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

    /// <summary>
    /// Regenerates the invoice PDF using the current active template.
    /// Useful when templates are updated and need to re-render existing invoices.
    /// </summary>
    [HttpPost("{id}/regenerate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RegenerateInvoicePdf(long id, CancellationToken cancellationToken)
    {
        var invoiceGenerationService = HttpContext.RequestServices.GetRequiredService<Application.Services.IInvoiceGenerationService>();
        
        try
        {
            // Use the existing service to regenerate the PDF with current template
            var pdfBytes = await invoiceGenerationService.GenerateInvoicePdfAsync(id, cancellationToken);
            
            _logger.LogInformation("Invoice {InvoiceId} PDF regenerated successfully", id);
            return Ok(new { message = "Invoice PDF regenerated successfully. Download the invoice to see the updated version." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to regenerate invoice PDF for {InvoiceId}", id);
            return StatusCode(500, new { error = "Failed to regenerate invoice PDF" });
        }
    }

    /// <summary>
    /// Regenerates the monthly report PDF using the current active template.
    /// Useful when monthly report templates are updated.
    /// </summary>
    [HttpPost("{id}/monthlyreport/regenerate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RegenerateMonthlyReportPdf(long id, CancellationToken cancellationToken)
    {
        var repository = HttpContext.RequestServices.GetRequiredService<IRepository<Core.Entities.Invoice>>();
        var invoice = await repository.GetByIdAsync(id, cancellationToken);
        
        if (invoice == null)
            return NotFound();

        if (invoice.Type != Core.Enums.InvoiceType.Monthly)
            return BadRequest(new { error = "Monthly reports are only available for monthly invoices" });

        try
        {
            // Monthly reports don't store rendered content - they're generated on-demand
            // So "regeneration" is just verification that it can be generated
            var customerRepository = HttpContext.RequestServices.GetRequiredService<IRepository<Core.Entities.Customer>>();
            var monthlyReportService = HttpContext.RequestServices.GetRequiredService<Application.Services.IMonthlyReportGenerationService>();
            
            var customer = await customerRepository.GetByIdAsync(invoice.CustomerId, cancellationToken);
            if (customer == null)
                return NotFound(new { error = "Customer not found" });

            // Test generation with current template
            _ = await monthlyReportService.GenerateReportHtmlAsync(invoice, customer, cancellationToken);

            _logger.LogInformation("Monthly report for invoice {InvoiceId} verified successfully", id);
            return Ok(new { message = "Monthly report is ready to be generated with current template" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Monthly report template not found for invoice {InvoiceId}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify monthly report for invoice {InvoiceId}", id);
            return StatusCode(500, new { error = "Failed to verify monthly report" });
        }
    }

    /// <summary>
    /// Gets the current global invoice sequence number.
    /// </summary>
    [HttpGet("sequence")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSequence(CancellationToken cancellationToken)
    {
        var repository = HttpContext.RequestServices.GetRequiredService<IRepository<InvoiceSequence>>();
        var sequence = await repository.GetByIdAsync(1, cancellationToken);

        if (sequence == null)
            return Ok(new { currentValue = 1, message = "Sequence not initialized yet" });

        return Ok(new { currentValue = sequence.CurrentValue });
    }

    /// <summary>
    /// Sets the global invoice sequence to a specific value.
    /// WARNING: Setting this too low can cause duplicate invoice numbers.
    /// </summary>
    [HttpPut("sequence")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetSequence(
        [FromBody] SetSequenceDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.Value < 1)
            return BadRequest(new { error = "Sequence value must be at least 1" });

        var repository = HttpContext.RequestServices.GetRequiredService<IRepository<InvoiceSequence>>();
        var unitOfWork = HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();

        var sequence = await repository.GetByIdAsync(1, cancellationToken);
        
        if (sequence == null)
        {
            sequence = new InvoiceSequence(dto.Value);
            await repository.AddAsync(sequence, cancellationToken);
        }
        else
        {
            sequence.SetValue(dto.Value);
            await repository.UpdateAsync(sequence, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Invoice sequence set to {Value}", dto.Value);
        return Ok(new { currentValue = sequence.CurrentValue, message = "Sequence updated successfully" });
    }
}

