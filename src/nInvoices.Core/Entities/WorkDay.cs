namespace nInvoices.Core.Entities;

/// <summary>
/// Represents a worked day for tracking in monthly invoices.
/// </summary>
public sealed class WorkDay : EntityBase
{
    public long CustomerId { get; set; }
    public DateOnly Date { get; set; }
    public string? Notes { get; set; }

    // Navigation property
    public Customer Customer { get; set; } = null!;

    public WorkDay()
    {
        CreatedAt = DateTime.UtcNow;
    }

    public WorkDay(long customerId, DateOnly date, string? notes = null) : this()
    {
        if (customerId <= 0)
            throw new ArgumentException("Customer ID must be positive", nameof(customerId));

        CustomerId = customerId;
        Date = date;
        Notes = notes;
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }
}
