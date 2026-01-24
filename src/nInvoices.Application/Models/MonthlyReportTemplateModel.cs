using nInvoices.Core.Enums;

namespace nInvoices.Application.Models;

/// <summary>
/// Model for rendering monthly report templates.
/// Contains all month days with their types and metadata.
/// All property names are in PascalCase (converted to camelCase by renderer).
/// </summary>
public sealed record MonthlyReportTemplateModel
{
    public string CustomerName { get; init; } = string.Empty;
    public string MonthDescription { get; init; } = string.Empty;
    public int Year { get; init; }
    public int MonthNumber { get; init; }
    
    public List<MonthDayTemplateModel> MonthDays { get; init; } = [];
    
    // Summary counters
    public int WorkedDaysCount { get; init; }
    public int PublicHolidayCount { get; init; }
    public int UnpaidLeaveCount { get; init; }
    public int TotalDaysInMonth { get; init; }
    
    // Optional financial data
    public decimal? DailyRate { get; init; }
    public string? Currency { get; init; }
    public decimal? TotalAmount { get; init; }
}

/// <summary>
/// Represents a single day in the monthly report.
/// </summary>
public sealed record MonthDayTemplateModel
{
    public string Date { get; init; } = string.Empty; // Formatted date (e.g., "24/01/2026")
    public string DayOfWeek { get; init; } = string.Empty; // e.g., "Monday"
    public int DayNumber { get; init; } // Day of month (1-31)
    public string Type { get; init; } = string.Empty; // "Worked", "PublicHoliday", "UnpaidLeave", or empty
    public bool IsWeekend { get; init; }
    public bool IsWorked { get; init; }
    public bool IsPublicHoliday { get; init; }
    public bool IsUnpaidLeave { get; init; }
    public string? Notes { get; init; }
}
