using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for a worked day entry.
/// Includes day type to distinguish between worked days, holidays, and leave.
/// For hourly rates, HoursWorked specifies the number of hours for that day.
/// </summary>
public sealed record WorkDayDto(
    DateOnly Date,
    DayType DayType = DayType.Worked,
    decimal? HoursWorked = null,
    string? Notes = null);