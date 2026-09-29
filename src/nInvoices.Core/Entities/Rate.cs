using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Core.Entities;

/// <summary>
/// Represents a rate (pricing) for a customer.
/// A customer can have any number of rates, including several of the same type (say two
/// hourly rates for different roles); the optional <see cref="Name"/> tells them apart.
/// </summary>
public sealed class Rate : OwnedEntityBase
{
    public long CustomerId { get; set; }
    public RateType Type { get; set; }
    public Money Price { get; set; } = null!;
    public bool IsActive { get; set; }

    /// <summary>What this rate is for, e.g. "Senior developer"; null when the price says it all.</summary>
    public string? Name { get; private set; }

    public const int MaxNameLength = 100;

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

    /// <param name="name">The name, or null/blank for none.</param>
    /// <exception cref="ArgumentException">The name is longer than <see cref="MaxNameLength"/>.</exception>
    public void SetName(string? name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (trimmed is { Length: > MaxNameLength })
            throw new ArgumentException($"The rate name must not exceed {MaxNameLength} characters");

        Name = trimmed;
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
