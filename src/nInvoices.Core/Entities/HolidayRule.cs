using nInvoices.Core.Enums;

namespace nInvoices.Core.Entities;

/// <summary>
/// One public holiday of a <see cref="HolidayCalendar"/>: its name and how its date is found
/// each year. Only the fields used by <see cref="Kind"/> are set; the others are null.
/// </summary>
public sealed class HolidayRule : EntityBase
{
    public long HolidayCalendarId { get; set; }
    public string Name { get; private set; } = string.Empty;
    public HolidayRuleKind Kind { get; private set; }

    /// <summary>1-12, for <see cref="HolidayRuleKind.Fixed"/> and <see cref="HolidayRuleKind.NthWeekday"/>.</summary>
    public int? Month { get; private set; }

    /// <summary>1-31, for <see cref="HolidayRuleKind.Fixed"/>.</summary>
    public int? Day { get; private set; }

    /// <summary>Days from Easter Sunday, for <see cref="HolidayRuleKind.EasterOffset"/>.</summary>
    public int? EasterOffset { get; private set; }

    /// <summary>For <see cref="HolidayRuleKind.NthWeekday"/>.</summary>
    public DayOfWeek? Weekday { get; private set; }

    /// <summary>1-5 for the first to fifth weekday of the month, -1 for the last, for <see cref="HolidayRuleKind.NthWeekday"/>.</summary>
    public int? Occurrence { get; private set; }

    /// <summary>First year the holiday applies (inclusive); null when it always did.</summary>
    public int? FromYear { get; private set; }

    /// <summary>Last year the holiday applies (inclusive); null when it still does.</summary>
    public int? ToYear { get; private set; }

    public bool IsActive { get; private set; } = true;

    public HolidayCalendar HolidayCalendar { get; set; } = null!;

    public HolidayRule()
    {
        CreatedAt = DateTime.UtcNow;
    }

    public static HolidayRule Fixed(string name, int month, int day, int? fromYear = null, int? toYear = null) =>
        Create(name, HolidayRuleKind.Fixed, month, day, null, null, null, fromYear, toYear);

    public static HolidayRule FromEaster(string name, int offset, int? fromYear = null, int? toYear = null) =>
        Create(name, HolidayRuleKind.EasterOffset, null, null, offset, null, null, fromYear, toYear);

    public static HolidayRule NthWeekdayOf(string name, int month, DayOfWeek weekday, int occurrence, int? fromYear = null, int? toYear = null) =>
        Create(name, HolidayRuleKind.NthWeekday, month, null, null, weekday, occurrence, fromYear, toYear);

    public static HolidayRule Create(
        string name,
        HolidayRuleKind kind,
        int? month,
        int? day,
        int? easterOffset,
        DayOfWeek? weekday,
        int? occurrence,
        int? fromYear,
        int? toYear)
    {
        var rule = new HolidayRule();
        rule.Define(name, kind, month, day, easterOffset, weekday, occurrence, fromYear, toYear);
        return rule;
    }

    /// <summary>Replaces the rule's definition, validating it for its kind.</summary>
    public void Define(
        string name,
        HolidayRuleKind kind,
        int? month,
        int? day,
        int? easterOffset,
        DayOfWeek? weekday,
        int? occurrence,
        int? fromYear,
        int? toYear)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Holiday name cannot be empty", nameof(name));
        if (fromYear.HasValue && toYear.HasValue && fromYear > toYear)
            throw new ArgumentException("The first year cannot be after the last year", nameof(fromYear));

        switch (kind)
        {
            case HolidayRuleKind.Fixed:
                if (month is not (>= 1 and <= 12))
                    throw new ArgumentException("Month must be between 1 and 12", nameof(month));
                // 29 February is allowed: it simply has no date in common years
                if (day is null || day < 1 || day > DateTime.DaysInMonth(2024, month.Value))
                    throw new ArgumentException("Day is not valid for the month", nameof(day));
                (easterOffset, weekday, occurrence) = (null, null, null);
                break;

            case HolidayRuleKind.EasterOffset:
                if (easterOffset is null or < -100 or > 100)
                    throw new ArgumentException("Days from Easter must be between -100 and 100", nameof(easterOffset));
                (month, day, weekday, occurrence) = (null, null, null, null);
                break;

            case HolidayRuleKind.NthWeekday:
                if (month is not (>= 1 and <= 12))
                    throw new ArgumentException("Month must be between 1 and 12", nameof(month));
                if (weekday is null || !Enum.IsDefined(weekday.Value))
                    throw new ArgumentException("Weekday is required", nameof(weekday));
                if (occurrence is not (-1 or (>= 1 and <= 5)))
                    throw new ArgumentException("Occurrence must be 1 to 5, or -1 for the last", nameof(occurrence));
                (day, easterOffset) = (null, null);
                break;

            default:
                throw new ArgumentException($"Unknown holiday rule kind {kind}", nameof(kind));
        }

        Name = name.Trim();
        Kind = kind;
        Month = month;
        Day = day;
        EasterOffset = easterOffset;
        Weekday = weekday;
        Occurrence = occurrence;
        FromYear = fromYear;
        ToYear = toYear;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// The holiday's date in <paramref name="year"/>, or null when it doesn't occur that year
    /// (outside <see cref="FromYear"/>-<see cref="ToYear"/>, 29 February in a common year, a fifth
    /// weekday the month doesn't have).
    /// </summary>
    public DateOnly? DateIn(int year)
    {
        if (year is < 1583 or > 9999)
            return null;
        if (FromYear.HasValue && year < FromYear.Value || ToYear.HasValue && year > ToYear.Value)
            return null;

        return Kind switch
        {
            HolidayRuleKind.Fixed when Day <= DateTime.DaysInMonth(year, Month!.Value) =>
                new DateOnly(year, Month.Value, Day!.Value),
            HolidayRuleKind.EasterOffset =>
                EasterSunday(year).AddDays(EasterOffset!.Value),
            HolidayRuleKind.NthWeekday =>
                NthWeekday(year, Month!.Value, Weekday!.Value, Occurrence!.Value),
            _ => null
        };
    }

    /// <summary>Easter Sunday in the Gregorian calendar (anonymous Gregorian algorithm).</summary>
    public static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }

    private static DateOnly? NthWeekday(int year, int month, DayOfWeek weekday, int occurrence)
    {
        if (occurrence == -1)
        {
            var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            return last.AddDays(-(((int)last.DayOfWeek - (int)weekday + 7) % 7));
        }

        var first = new DateOnly(year, month, 1);
        var date = first.AddDays(((int)weekday - (int)first.DayOfWeek + 7) % 7 + 7 * (occurrence - 1));
        return date.Month == month ? date : null;
    }
}
