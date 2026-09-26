namespace nInvoices.Core.Interfaces;

/// <summary>
/// Defines a tax calculation handler using the Strategy pattern.
/// Implementations can provide different tax calculation logic.
/// </summary>
public interface ITaxHandler
{
    /// <summary>
    /// Unique identifier for this handler (e.g., "PERCENTAGE", "FIXED")
    /// </summary>
    string HandlerId { get; }

    /// <summary>
    /// Human-readable description of what this handler does
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Calculates the tax amount.
    /// </summary>
    /// <param name="baseAmount">The amount to apply tax to</param>
    /// <param name="rate">The tax rate (meaning depends on handler)</param>
    /// <param name="context">Additional context data for complex calculations</param>
    /// <returns>The calculated tax amount</returns>
    decimal Calculate(decimal baseAmount, decimal rate, IDictionary<string, decimal>? context = null);
}
