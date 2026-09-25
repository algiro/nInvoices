using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Holidays.Commands;

/// <summary>
/// Replaces a country's rules with the built-in ones, dropping every edit. Null when the country
/// has no built-in rules.
/// </summary>
public sealed record ResetHolidayCalendarCommand(string CountryCode, int Year) : IRequest<HolidayCalendarDto?>;
