using nInvoices.Core.ValueObjects;

namespace nInvoices.Core.Entities;

/// <summary>
/// Represents an expense to be included in an invoice.
/// </summary>
public sealed class Expense : EntityBase
{
    public long CustomerId { get; set; }
    public long? InvoiceId { get; set; }
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public Money Amount { get; set; } = null!;

    // Navigation properties
    public Customer Customer { get; set; } = null!;
    public Invoice? Invoice { get; set; }

    public Expense()
    {
        CreatedAt = DateTime.UtcNow;
    }

    public Expense(
        long customerId,
        DateOnly date,
        string description,
        Money amount) : this()
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(amount);

        if (customerId <= 0)
            throw new ArgumentException("Customer ID must be positive", nameof(customerId));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));

        CustomerId = customerId;
        Date = date;
        Description = description;
        Amount = amount;
    }

    public void Update(string description, Money amount)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(amount);

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));

        Description = description;
        Amount = amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignToInvoice(long invoiceId)
    {
        if (invoiceId <= 0)
            throw new ArgumentException("Invoice ID must be positive", nameof(invoiceId));

        InvoiceId = invoiceId;
        UpdatedAt = DateTime.UtcNow;
    }
}
