using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Holidays.Queries;

/// <summary>Countries with built-in holiday rules or a stored calendar, by name.</summary>
public sealed record GetHolidayCountriesQuery : IRequest<IReadOnlyList<HolidayCountryDto>>;
