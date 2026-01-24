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
    public CustomerTemplateModel? Customer { get; init; } // Nested customer object for template access
    public string Locale { get; init; } = "en-US"; // Locale for localization (e.g., "it-IT", "en-US", "es-ES")
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
    public DateTime DateValue { get; init; } // DateTime object for Scriban date filters and localization
    public int DayNumber { get; init; } // Day of month (1-31)
    public string Type { get; init; } = string.Empty; // "Worked", "PublicHoliday", "UnpaidLeave", or empty
    public bool IsWeekend { get; init; }
    public bool IsWorked { get; init; }
    public bool IsPublicHoliday { get; init; }
    public bool IsUnpaidLeave { get; init; }
    public string? Notes { get; init; }
}
