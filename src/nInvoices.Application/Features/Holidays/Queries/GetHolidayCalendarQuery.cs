using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Holidays.Queries;

/// <summary>
/// A country's holiday rules and the holidays they give in <paramref name="Year"/>; null for a
/// country with no built-in rules and no stored calendar. The first request for a country with
/// built-in rules stores its calendar, so it can be edited from then on.
/// </summary>
public sealed record GetHolidayCalendarQuery(string CountryCode, int Year) : IRequest<HolidayCalendarDto?>;
