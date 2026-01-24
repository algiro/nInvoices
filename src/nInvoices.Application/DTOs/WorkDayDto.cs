using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for a worked day entry.
/// Includes day type to distinguish between worked days, holidays, and leave.
/// </summary>
public sealed record WorkDayDto(
    DateOnly Date,
    DayType DayType = DayType.Worked,
    string? Notes = null);