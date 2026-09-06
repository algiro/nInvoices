using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for a worked day entry.
/// Includes day type to distinguish between worked days, holidays, and leave.
/// <see cref="Projects"/> breaks the day down by project; when present, the day's total
/// hours are the sum of the allocations and drive hourly-rate billing.
/// </summary>
public sealed record WorkDayDto(
    DateOnly Date,
    DayType DayType = DayType.Worked,
    decimal? HoursWorked = null,
    string? Notes = null,
    IReadOnlyList<WorkDayProjectDto>? Projects = null);
