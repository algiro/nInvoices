using nInvoices.Core.Enums;

namespace nInvoices.Core.Entities;

/// <summary>
/// Represents a worked day for tracking in monthly invoices.
/// Includes day type to distinguish between worked days, holidays, and leave.
/// </summary>
public sealed class WorkDay : EntityBase
{
    public long CustomerId { get; set; }
    public DateOnly Date { get; set; }
    public DayType DayType { get; set; }
    public decimal? HoursWorked { get; set; }
    public string? Notes { get; set; }

    // Navigation property
    public Customer Customer { get; set; } = null!;

    public WorkDay()
    {
        CreatedAt = DateTime.UtcNow;
        DayType = DayType.Worked;
    }

    public WorkDay(long customerId, DateOnly date, DayType dayType = DayType.Worked, decimal? hoursWorked = null, string? notes = null) : this()
    {
        if (customerId <= 0)
            throw new ArgumentException("Customer ID must be positive", nameof(customerId));

        if (hoursWorked.HasValue && hoursWorked.Value <= 0)
            throw new ArgumentException("Hours worked must be positive", nameof(hoursWorked));

        CustomerId = customerId;
        Date = date;
        DayType = dayType;
        HoursWorked = hoursWorked;
        Notes = notes;
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void UpdateDayType(DayType dayType)
    {
        DayType = dayType;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void UpdateHoursWorked(decimal? hoursWorked)
    {
        if (hoursWorked.HasValue && hoursWorked.Value <= 0)
            throw new ArgumentException("Hours worked must be positive", nameof(hoursWorked));
        
        HoursWorked = hoursWorked;
        UpdatedAt = DateTime.UtcNow;
    }
}
