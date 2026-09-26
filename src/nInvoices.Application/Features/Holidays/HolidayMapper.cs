using nInvoices.Application.DTOs;
using nInvoices.Application.Services.Holidays;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Features.Holidays;

internal static class HolidayMapper
{
    public static HolidayRuleDto ToDto(HolidayRule rule) => new(
        rule.Id,
        rule.Name,
        rule.Kind,
        rule.Month,
        rule.Day,
        rule.EasterOffset,
        rule.Weekday,
        rule.Occurrence,
        rule.FromYear,
        rule.ToYear,
        rule.IsActive);

    public static HolidayCalendarDto ToDto(HolidayCalendar calendar, int year) => new(
        calendar.CountryCode,
        CountryCodes.DisplayName(calendar.CountryCode),
        BuiltInHolidays.Has(calendar.CountryCode),
        calendar.Rules.OrderBy(r => SortKey(r, year)).ThenBy(r => r.Name).Select(ToDto).ToList(),
        year,
        calendar.HolidaysIn(year).Select(h => new PublicHolidayDto(h.Date, h.Name)).ToList());

    /// <summary>Applies a request to a rule; throws <see cref="ArgumentException"/> when it isn't valid for its kind.</summary>
    public static void Apply(HolidayRule rule, SaveHolidayRuleDto dto)
    {
        rule.Define(dto.Name, dto.Kind, dto.Month, dto.Day, dto.EasterOffset, dto.Weekday, dto.Occurrence, dto.FromYear, dto.ToYear);
        if (dto.IsActive)
            rule.Activate();
        else
            rule.Deactivate();
    }

    // Rules are listed in calendar order: by the date they give in the year shown, or by their
    // month and day when they don't apply that year
    private static (int Month, int Day) SortKey(HolidayRule rule, int year) =>
        rule.DateIn(year) is { } date ? (date.Month, date.Day) : (rule.Month ?? 13, rule.Day ?? 1);
}
