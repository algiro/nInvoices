using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Compliance.Commands;
using nInvoices.Application.Features.Compliance.Queries;

namespace nInvoices.Api.Controllers;

/// <summary>
/// The country invoicing regimes (tax compliance) this installation offers, and the current
/// user setup of each. Everything is off until the user turns a country on.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ComplianceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ComplianceController> _logger;

    public ComplianceController(IMediator mediator, ILogger<ComplianceController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Lists the offered countries with what each involves and the user settings. Empty when
    /// the installation offers none.
    /// </summary>
    [HttpGet("countries")]
    [ProducesResponseType(typeof(IReadOnlyList<ComplianceCountryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ComplianceCountryDto>>> GetCountries(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetComplianceCountriesQuery(), cancellationToken));
    }

    /// <summary>
    /// Stores the user signing certificate (a .p12/.pfx file, base64, with its password) for a country.
    /// It is checked and encrypted; it is never returned.
    /// </summary>
    [HttpPut("countries/{countryCode}/certificate")]
    [ProducesResponseType(typeof(ComplianceCountryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComplianceCountryDto>> SetCertificate(
        string countryCode,
        [FromBody] UploadCertificateDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var country = await _mediator.Send(new SetSigningCertificateCommand(countryCode, dto), cancellationToken);
            if (country is null)
                return NotFound();

            _logger.LogInformation("Stored the {Country} signing certificate", country.CountryCode);
            return Ok(country);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Removes the stored signing certificate of a country.</summary>
    [HttpDelete("countries/{countryCode}/certificate")]
    [ProducesResponseType(typeof(ComplianceCountryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComplianceCountryDto>> RemoveCertificate(string countryCode, CancellationToken cancellationToken)
    {
        var country = await _mediator.Send(new RemoveSigningCertificateCommand(countryCode), cancellationToken);
        return country is null ? NotFound() : Ok(country);
    }

    /// <summary>
    /// Saves the user settings for a country. Turning it on checks them against the country rules.
    /// </summary>
    [HttpPut("countries/{countryCode}")]
    [ProducesResponseType(typeof(ComplianceCountryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComplianceCountryDto>> UpdateCountry(
        string countryCode,
        [FromBody] UpdateComplianceSettingsDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var country = await _mediator.Send(new UpdateComplianceSettingsCommand(countryCode, dto), cancellationToken);
            if (country is null)
                return NotFound();

            _logger.LogInformation("Saved {Country} compliance settings (enabled: {Enabled})", country.CountryCode, country.Settings.IsEnabled);
            return Ok(country);
        }
        catch (ComplianceValidationException ex)
        {
            return BadRequest(new { error = ex.Message, issues = ex.Issues });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
