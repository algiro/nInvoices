using nInvoices.Core.Entities;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Core.Interfaces;

/// <summary>
/// Service interface for tax calculations.
/// Part of Core layer - can be used by Application layer.
/// </summary>
public interface ITaxCalculationService
{
    (Money TotalTax, IEnumerable<InvoiceTaxLine> TaxLines) CalculateTaxes(
        IEnumerable<Tax> customerTaxes,
        Money subtotal);
}
