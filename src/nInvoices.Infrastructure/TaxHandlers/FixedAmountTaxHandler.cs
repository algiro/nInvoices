using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.TaxHandlers;

/// <summary>
/// Applies a fixed tax amount regardless of the base amount.
/// Supports negative amounts for fixed deductions/withholdings.
/// Example: €50 flat administrative fee, -€100 fixed deduction
/// Rate parameter is treated as the fixed amount.
/// </summary>
public sealed class FixedAmountTaxHandler : ITaxHandler
{
    public string HandlerId => "FIXED";
    
    public string Description => "Fixed amount tax (flat fee)";

    public decimal Calculate(decimal baseAmount, decimal rate, IDictionary<string, decimal>? context = null)
    {
        // Rate represents the fixed amount
        return rate;
    }
}
