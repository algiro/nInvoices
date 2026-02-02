using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using nInvoices.Core.Configuration;

namespace nInvoices.Api.Controllers;

/// <summary>
/// API controller for managing application settings.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class SettingsController : ControllerBase
{
    private readonly IOptionsSnapshot<InvoiceSettings> _invoiceSettings;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        IOptionsSnapshot<InvoiceSettings> invoiceSettings,
        ILogger<SettingsController> logger)
    {
        _invoiceSettings = invoiceSettings;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current invoice settings.
    /// </summary>
    [HttpGet("invoice")]
    [ProducesResponseType(typeof(InvoiceSettingsDto), StatusCodes.Status200OK)]
    public ActionResult<InvoiceSettingsDto> GetInvoiceSettings()
    {
        var settings = _invoiceSettings.Value;
        var dto = new InvoiceSettingsDto
        {
            NumberFormat = settings.NumberFormat,
            FirstDayOfWeek = settings.FirstDayOfWeek
        };

        return Ok(dto);
    }
}

/// <summary>
/// DTO for invoice settings.
/// </summary>
public sealed record InvoiceSettingsDto
{
    public required string NumberFormat { get; init; }
    public required int FirstDayOfWeek { get; init; }
}
