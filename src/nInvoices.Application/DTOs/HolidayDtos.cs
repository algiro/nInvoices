using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>A rule of a country's holiday calendar. Only the fields used by <see cref="Kind"/> are set.</summary>
public sealed record HolidayRuleDto(
    long Id,
    string Name,
    HolidayRuleKind Kind,
    int? Month,
    int? Day,
    int? EasterOffset,
    DayOfWeek? Weekday,
    int? Occurrence,
    int? FromYear,
    int? ToYear,
    bool IsActive);

/// <summary>Request to add or change a holiday rule.</summary>
public sealed record SaveHolidayRuleDto(
    string Name,
    HolidayRuleKind Kind,
    int? Month = null,
    int? Day = null,
    int? EasterOffset = null,
    DayOfWeek? Weekday = null,
    int? Occurrence = null,
    int? FromYear = null,
    int? ToYear = null,
    bool IsActive = true);

/// <summary>A public holiday on a date.</summary>
public sealed record PublicHolidayDto(DateOnly Date, string Name);

/// <summary>A country's calendar: its rules and, to check them, the holidays they give in <see cref="Year"/>.</summary>
public sealed record HolidayCalendarDto(
    string CountryCode,
    string CountryName,
    bool HasBuiltIn,
    IReadOnlyList<HolidayRuleDto> Rules,
    int Year,
    IReadOnlyList<PublicHolidayDto> Holidays);

/// <summary>A country that has built-in rules or a stored calendar.</summary>
public sealed record HolidayCountryDto(string CountryCode, string CountryName, bool HasBuiltIn, bool HasCalendar);

/// <summary>The public holidays that apply to a customer in a period.</summary>
/// <param name="CountryCode">The customer's holiday country; null when it can't be determined.</param>
public sealed record CustomerHolidaysDto(string? CountryCode, string? CountryName, IReadOnlyList<PublicHolidayDto> Holidays);
