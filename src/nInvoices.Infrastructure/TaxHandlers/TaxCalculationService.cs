using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Infrastructure.TaxHandlers;

/// <summary>
/// Service for calculating taxes on invoices.
/// Orchestrates multiple tax handlers and manages tax application order.
/// </summary>
public sealed class TaxCalculationService : ITaxCalculationService
{
    private readonly IEnumerable<ITaxHandler> _taxHandlers;

    public TaxCalculationService(IEnumerable<ITaxHandler> taxHandlers)
    {
        _taxHandlers = taxHandlers ?? throw new ArgumentNullException(nameof(taxHandlers));
    }

    /// <summary>
    /// Calculates all taxes for an invoice based on customer tax configuration.
    /// Returns the total tax amount and individual tax line items.
    /// </summary>
    public (Money TotalTax, IEnumerable<InvoiceTaxLine> TaxLines) CalculateTaxes(
        IEnumerable<Tax> customerTaxes,
        Money subtotal)
    {
        ArgumentNullException.ThrowIfNull(customerTaxes);
        ArgumentNullException.ThrowIfNull(subtotal);

        var activeTaxes = customerTaxes
            .Where(t => t.IsActive)
            .OrderBy(t => t.Order)
            .ToList();

        if (!activeTaxes.Any())
            return (Money.Zero(subtotal.Currency), Array.Empty<InvoiceTaxLine>());

        var taxLines = new List<InvoiceTaxLine>();
        var calculatedTaxes = new Dictionary<long, Money>();

        foreach (var tax in activeTaxes)
        {
            var handler = GetHandler(tax.HandlerId);
            var baseAmount = GetBaseAmount(tax, subtotal, calculatedTaxes);
            
            var context = CreateContext(tax, calculatedTaxes);
            var taxAmount = handler.Calculate(baseAmount.Amount, tax.Rate, context);

            var taxMoney = new Money(taxAmount, subtotal.Currency);
            calculatedTaxes[tax.Id] = taxMoney;

            var taxLine = new InvoiceTaxLine(
                tax.TaxId,
                tax.Description,
                tax.Rate,
                baseAmount,
                taxMoney,
                tax.Order);
            taxLines.Add(taxLine);
        }

        var totalTax = calculatedTaxes.Values.Aggregate(Money.Zero(subtotal.Currency), (acc, tax) => acc + tax);

        return (totalTax, taxLines);
    }

    /// <summary>
    /// Gets all available tax handlers.
    /// </summary>
    public IEnumerable<ITaxHandler> GetAvailableHandlers() => _taxHandlers;

    private ITaxHandler GetHandler(string handlerId)
    {
        var handler = _taxHandlers.FirstOrDefault(h => 
            h.HandlerId.Equals(handlerId, StringComparison.OrdinalIgnoreCase));

        if (handler is null)
            throw new InvalidOperationException($"Tax handler '{handlerId}' not found");

        return handler;
    }

    private static Money GetBaseAmount(
        Tax tax,
        Money subtotal,
        Dictionary<long, Money> calculatedTaxes)
    {
        return tax.ApplicationType switch
        {
            TaxApplicationType.OnSubtotal => subtotal,
            TaxApplicationType.OnTax when tax.AppliedToTaxId.HasValue =>
                calculatedTaxes.TryGetValue(tax.AppliedToTaxId.Value, out var baseTax)
                    ? baseTax
                    : throw new InvalidOperationException(
                        $"Tax '{tax.TaxId}' references non-existent or not-yet-calculated tax ID {tax.AppliedToTaxId}"),
            _ => throw new InvalidOperationException($"Invalid tax application type for tax '{tax.TaxId}'")
        };
    }

    private static Dictionary<string, decimal>? CreateContext(
        Tax tax,
        Dictionary<long, Money> calculatedTaxes)
    {
        if (tax.ApplicationType != TaxApplicationType.OnTax || !tax.AppliedToTaxId.HasValue)
            return null;

        if (!calculatedTaxes.TryGetValue(tax.AppliedToTaxId.Value, out var baseTax))
            return null;

        return new Dictionary<string, decimal>
        {
            ["BaseTaxAmount"] = baseTax.Amount
        };
    }
}
