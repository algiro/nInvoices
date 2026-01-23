using nInvoices.Core.Enums;

namespace nInvoices.Core.Entities;

/// <summary>
/// Represents a tax configuration for a customer.
/// Supports different tax calculation strategies via handler system.
/// </summary>
public sealed class Tax : EntityBase
{
    public long CustomerId { get; set; }
    public string TaxId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HandlerId { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public TaxApplicationType ApplicationType { get; set; }
    public long? AppliedToTaxId { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; }

    // Navigation properties
    public Customer Customer { get; set; } = null!;
    public Tax? AppliedToTax { get; set; }

    public Tax()
    {
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
    }

    public Tax(
        long customerId,
        string taxId,
        string description,
        string handlerId,
        decimal rate,
        TaxApplicationType applicationType,
        int order = 0) : this()
    {
        ArgumentNullException.ThrowIfNull(taxId);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(handlerId);

        if (customerId <= 0)
            throw new ArgumentException("Customer ID must be positive", nameof(customerId));
        if (string.IsNullOrWhiteSpace(taxId))
            throw new ArgumentException("Tax ID cannot be empty", nameof(taxId));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));
        if (string.IsNullOrWhiteSpace(handlerId))
            throw new ArgumentException("Handler ID cannot be empty", nameof(handlerId));

        CustomerId = customerId;
        TaxId = taxId;
        Description = description;
        HandlerId = handlerId;
        Rate = rate;
        ApplicationType = applicationType;
        Order = order;
    }

    public void Update(string description, decimal rate)
    {
        ArgumentNullException.ThrowIfNull(description);

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));

        Description = description;
        Rate = rate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCompoundTax(long appliedToTaxId)
    {
        if (appliedToTaxId <= 0)
            throw new ArgumentException("Applied to tax ID must be positive", nameof(appliedToTaxId));

        ApplicationType = TaxApplicationType.OnTax;
        AppliedToTaxId = appliedToTaxId;
        UpdatedAt = DateTime.UtcNow;
    }
}
