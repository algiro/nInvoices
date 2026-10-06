using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.ImportExport;

namespace nInvoices.Api.Controllers;

/// <summary>
/// Import/Export controller for data migration and backups.
/// Exports/imports customers (with rates, taxes, templates), invoices and settings as JSON.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ImportExportController : ControllerBase
{
    private readonly IMediator _mediator;

    public ImportExportController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Export all customers with their rates, taxes, templates, projects, worked days and unbilled expenses.
    /// </summary>
    [HttpGet("customers")]
    [ProducesResponseType(typeof(DataExportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DataExportDto>> ExportCustomers(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new ExportCustomersQuery(), cancellationToken));

    /// <summary>
    /// Export invoices with optional filters.
    /// </summary>
    [HttpGet("invoices")]
    [ProducesResponseType(typeof(DataExportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DataExportDto>> ExportInvoices(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] long? customerId,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new ExportInvoicesQuery(year, month, customerId), cancellationToken));

    /// <summary>
    /// Import customers with their rates, taxes, templates, and monthly report templates.
    /// Customers are matched by FiscalId - existing customers are skipped.
    /// </summary>
    [HttpPost("customers")]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportResultDto>> ImportCustomers(
        [FromBody] DataExportDto data,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new ImportCustomersCommand(data), cancellationToken));

    /// <summary>
    /// Import invoices. Customers must already exist (matched by FiscalId).
    /// </summary>
    [HttpPost("invoices")]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportResultDto>> ImportInvoices(
        [FromBody] DataExportDto data,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new ImportInvoicesCommand(data), cancellationToken));

    /// <summary>
    /// Export the user's settings and assets: invoice numbering, images used by templates, the
    /// holiday calendars they changed and their e-invoicing settings (without the signing certificate).
    /// </summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(DataExportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DataExportDto>> ExportSettings(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new ExportSettingsQuery(), cancellationToken));

    /// <summary>
    /// Import settings and assets. What the user already has is kept: an image with the same name, a
    /// calendar for the same country, e-invoicing settings for the same country. The invoice sequence
    /// only moves forward, so restoring never hands out a number twice.
    /// </summary>
    [HttpPost("settings")]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportResultDto>> ImportSettings(
        [FromBody] DataExportDto data,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new ImportSettingsCommand(data), cancellationToken));
}
