using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.TaxHandlers;

/// <summary>
/// Applies a fixed tax amount regardless of the base amount.
/// Example: €50 flat administrative fee
/// Rate parameter is treated as the fixed amount.
/// </summary>
public sealed class FixedAmountTaxHandler : ITaxHandler
{
    public string HandlerId => "FIXED";
    
    public string Description => "Fixed amount tax (flat fee)";

    public decimal Calculate(decimal baseAmount, decimal rate, IDictionary<string, decimal>? context = null)
    {
        if (rate < 0)
            throw new ArgumentException("Fixed amount cannot be negative", nameof(rate));

        // Rate represents the fixed amount
        return rate;
    }
}
