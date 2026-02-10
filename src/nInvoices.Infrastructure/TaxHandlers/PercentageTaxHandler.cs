using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.TaxHandlers;

/// <summary>
/// Calculates tax as a percentage of the base amount.
/// Supports negative rates for withholdings/deductions.
/// Example: 21% VAT on €100 = €21, -15% withholding on €3900 = -€585
/// </summary>
public sealed class PercentageTaxHandler : ITaxHandler
{
    public string HandlerId => "PERCENTAGE";
    
    public string Description => "Percentage-based tax (e.g., VAT, Sales Tax)";

    public decimal Calculate(decimal baseAmount, decimal rate, IDictionary<string, decimal>? context = null)
    {
        if (baseAmount < 0)
            throw new ArgumentException("Base amount cannot be negative", nameof(baseAmount));
        
        return baseAmount * (rate / 100m);
    }
}
