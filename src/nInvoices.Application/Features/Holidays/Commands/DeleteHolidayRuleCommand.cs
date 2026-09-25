using MediatR;

namespace nInvoices.Application.Features.Holidays.Commands;

/// <summary>Removes a holiday rule; false when it doesn't exist.</summary>
public sealed record DeleteHolidayRuleCommand(long Id) : IRequest<bool>;
