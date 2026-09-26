using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Holidays.Commands;

/// <summary>Changes a holiday rule; null when it doesn't exist.</summary>
public sealed record UpdateHolidayRuleCommand(long Id, SaveHolidayRuleDto Rule) : IRequest<HolidayRuleDto?>;
