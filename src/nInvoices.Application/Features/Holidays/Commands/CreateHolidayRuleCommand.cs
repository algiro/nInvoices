using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Holidays.Commands;

/// <summary>Adds a rule to a country's calendar, creating the calendar when the country has none.</summary>
public sealed record CreateHolidayRuleCommand(string CountryCode, SaveHolidayRuleDto Rule) : IRequest<HolidayRuleDto>;
