using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.TaxHandlers;

/// <summary>
/// Calculates tax as a percentage of the base amount.
/// Example: 21% VAT on €100 = €21
/// </summary>
public sealed class PercentageTaxHandler : ITaxHandler
{
    public string HandlerId => "PERCENTAGE";
    
    public string Description => "Percentage-based tax (e.g., VAT, Sales Tax)";

    public decimal Calculate(decimal baseAmount, decimal rate, IDictionary<string, decimal>? context = null)
    {
        if (baseAmount < 0)
            throw new ArgumentException("Base amount cannot be negative", nameof(baseAmount));
        
        if (rate < 0)
            throw new ArgumentException("Rate cannot be negative", nameof(rate));

        return baseAmount * (rate / 100m);
    }
}
