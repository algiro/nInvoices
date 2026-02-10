using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.TaxHandlers;

/// <summary>
/// Calculates tax on another tax (compound/cascading tax).
/// Supports negative rates for withholdings/deductions on taxes.
/// Requires context with the base tax amount.
/// Example: 10% luxury tax on top of 21% VAT
/// If VAT = €21, then luxury tax = €21 * 10% = €2.10
/// </summary>
public sealed class CompoundTaxHandler : ITaxHandler
{
    public string HandlerId => "COMPOUND";
    
    public string Description => "Compound tax (tax on tax)";

    public decimal Calculate(decimal baseAmount, decimal rate, IDictionary<string, decimal>? context = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        // baseAmount represents the tax we're applying this compound tax to
        if (!context.TryGetValue("BaseTaxAmount", out var baseTaxAmount))
            throw new InvalidOperationException(
                "CompoundTaxHandler requires 'BaseTaxAmount' in context");

        if (baseTaxAmount < 0)
            throw new ArgumentException("Base tax amount cannot be negative");

        return baseTaxAmount * (rate / 100m);
    }
}
