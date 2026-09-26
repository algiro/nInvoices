using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Holidays.Commands;
using nInvoices.Application.Features.Holidays.Queries;

namespace nInvoices.Api.Controllers;

/// <summary>
/// Public holiday calendars per country (built-in rules, editable) and the holidays that apply
/// to a customer, used to mark public holidays on time sheets.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class HolidaysController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<HolidaysController> _logger;

    public HolidaysController(IMediator mediator, ILogger<HolidaysController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>Countries with built-in rules or a stored calendar.</summary>
    [HttpGet("countries")]
    [ProducesResponseType(typeof(IReadOnlyList<HolidayCountryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HolidayCountryDto>>> GetCountries(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetHolidayCountriesQuery(), cancellationToken));

    /// <summary>The public holidays of the customer's holiday country in a month (or a year when month is omitted).</summary>
    [HttpGet("customer/{customerId:long}")]
    [ProducesResponseType(typeof(CustomerHolidaysDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerHolidaysDto>> GetForCustomer(
        long customerId,
        [FromQuery] int year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        var holidays = await _mediator.Send(new GetCustomerHolidaysQuery(customerId, year, month), cancellationToken);
        return holidays is null ? NotFound() : Ok(holidays);
    }

    /// <summary>A country's rules and the holidays they give in a year (default: this year).</summary>
    [HttpGet("{countryCode:length(2)}")]
    [ProducesResponseType(typeof(HolidayCalendarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HolidayCalendarDto>> GetCalendar(
        string countryCode,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        try
        {
            var calendar = await _mediator.Send(
                new GetHolidayCalendarQuery(countryCode, year ?? DateTime.Today.Year), cancellationToken);
            return calendar is null ? NotFound() : Ok(calendar);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Adds a rule to a country's calendar (creating the calendar if needed).</summary>
    [HttpPost("{countryCode:length(2)}/rules")]
    [ProducesResponseType(typeof(HolidayRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HolidayRuleDto>> CreateRule(
        string countryCode,
        [FromBody] SaveHolidayRuleDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var rule = await _mediator.Send(new CreateHolidayRuleCommand(countryCode, dto), cancellationToken);
            _logger.LogInformation("Added holiday rule {RuleId} to {CountryCode}", rule.Id, countryCode);
            return Ok(rule);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Changes a holiday rule.</summary>
    [HttpPut("rules/{id:long}")]
    [ProducesResponseType(typeof(HolidayRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HolidayRuleDto>> UpdateRule(
        long id,
        [FromBody] SaveHolidayRuleDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var rule = await _mediator.Send(new UpdateHolidayRuleCommand(id, dto), cancellationToken);
            return rule is null ? NotFound() : Ok(rule);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Removes a holiday rule.</summary>
    [HttpDelete("rules/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteRule(long id, CancellationToken cancellationToken) =>
        await _mediator.Send(new DeleteHolidayRuleCommand(id), cancellationToken) ? NoContent() : NotFound();

    /// <summary>Replaces a country's rules with the built-in ones.</summary>
    [HttpPost("{countryCode:length(2)}/reset")]
    [ProducesResponseType(typeof(HolidayCalendarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HolidayCalendarDto>> Reset(
        string countryCode,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        try
        {
            var calendar = await _mediator.Send(
                new ResetHolidayCalendarCommand(countryCode, year ?? DateTime.Today.Year), cancellationToken);
            if (calendar is null)
                return NotFound(new { message = $"There are no built-in holidays for {countryCode.ToUpperInvariant()}" });

            _logger.LogInformation("Holiday calendar {CountryCode} reset to the built-in rules", calendar.CountryCode);
            return Ok(calendar);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
