namespace nInvoices.Core.Enums;

/// <summary>
/// Defines how a tax is applied.
/// </summary>
public enum TaxApplicationType
{
    /// <summary>
    /// Tax is applied to the invoice subtotal
    /// </summary>
    OnSubtotal,

    /// <summary>
    /// Tax is applied to another tax (compound tax)
    /// </summary>
    OnTax
}
