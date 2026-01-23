using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Core.Entities;

/// <summary>
/// Represents a rate (pricing) for a customer.
/// Each customer can have multiple rates of different types.
/// </summary>
public sealed class Rate : EntityBase
{
    public long CustomerId { get; set; }
    public RateType Type { get; set; }
    public Money Price { get; set; } = null!;
    public bool IsActive { get; set; }

    // Navigation property
    public Customer Customer { get; set; } = null!;

    public Rate()
    {
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
    }

    public Rate(long customerId, RateType type, Money price) : this()
    {
        ArgumentNullException.ThrowIfNull(price);

        if (customerId <= 0)
            throw new ArgumentException("Customer ID must be positive", nameof(customerId));

        CustomerId = customerId;
        Type = type;
        Price = price;
    }

    public void UpdatePrice(Money newPrice)
    {
        ArgumentNullException.ThrowIfNull(newPrice);
        Price = newPrice;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
