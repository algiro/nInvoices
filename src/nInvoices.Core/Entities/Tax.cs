using nInvoices.Core.Compliance;
using nInvoices.Core.Enums;

namespace nInvoices.Core.Entities;

/// <summary>
/// Represents a tax configuration for a customer.
/// Supports different tax calculation strategies via handler system.
/// </summary>
public sealed class Tax : OwnedEntityBase
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

    /// <summary>
    /// Extra data country invoicing regimes ask for about this tax, keyed "COUNTRY.field"
    /// (e.g. "ES.operation" for how a 0% VAT is treated).
    /// </summary>
    public Dictionary<string, string> ComplianceValues { get; private set; } = new();

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

    /// <summary>One country values, without the country prefix.</summary>
    public IReadOnlyDictionary<string, string> GetComplianceValues(string countryCode) =>
        ComplianceValueMap.For(ComplianceValues, countryCode);

    /// <summary>Replaces the values of one country, leaving the other countries alone.</summary>
    public void SetComplianceValues(string countryCode, IReadOnlyDictionary<string, string> values)
    {
        ComplianceValues = ComplianceValueMap.Replace(ComplianceValues, countryCode, values);
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
