namespace nInvoices.Core.Enums;

/// <summary>
/// Represents the type of day in a monthly invoice/report.
/// Used to categorize days as worked, holidays, or leave.
/// </summary>
public enum DayType
{
    /// <summary>
    /// Regular worked day
    /// </summary>
    Worked = 0,
    
    /// <summary>
    /// Public holiday (paid)
    /// </summary>
    PublicHoliday = 1,
    
    /// <summary>
    /// Unpaid leave day
    /// </summary>
    UnpaidLeave = 2
}
