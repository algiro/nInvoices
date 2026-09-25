namespace nInvoices.Core.Enums;

/// <summary>
/// How a public holiday's date is found in a given year.
/// </summary>
public enum HolidayRuleKind
{
    /// <summary>The same day and month every year, e.g. 25 December.</summary>
    Fixed = 0,

    /// <summary>A number of days from Easter Sunday, e.g. Easter Monday (+1) or Good Friday (-2).</summary>
    EasterOffset = 1,

    /// <summary>The nth (or last) weekday of a month, e.g. the last Monday of May.</summary>
    NthWeekday = 2
}
