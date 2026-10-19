using nInvoices.Application.Compliance;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Countries.UnitedKingdom;

/// <summary>The VAT of an invoice as the United Kingdom's rules see it, with the user's UK settings.</summary>
/// <param name="Issuer">The user's UK settings, or null when the UK rules don't apply to them.</param>
/// <param name="Vat">The invoice's VAT lines (added taxes), with the treatment the user gave each tax at 0%.</param>
public sealed record UkInvoiceVat(ComplianceSettings? Issuer, IReadOnlyList<(InvoiceTaxLine Line, string? Treatment)> Vat)
{
    public bool IsVatRegistered => Issuer is not null && UkComplianceModule.IsVatRegistered(Issuer.Values);

    /// <summary>Why the invoice charges no VAT, from the first 0% line with a treatment; null if it charges VAT or doesn't say.</summary>
    public string? Note =>
        Vat.Count > 0 && Vat.All(v => v.Line.TaxAmount.Amount == 0)
            ? Vat.Select(v => v.Treatment).FirstOrDefault(t => t is not null) is { } treatment
                ? UkComplianceModule.TreatmentNotes.GetValueOrDefault(treatment)
                : null
            : null;

    public static async Task<UkInvoiceVat> OfAsync(Invoice invoice, IComplianceGate gate, IRepository<Tax> taxes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(taxes);

        var uk = (await gate.GetActiveAsync(cancellationToken))
            .FirstOrDefault(a => a.Module.CountryCode == UkComplianceModule.CountryCodeValue);
        if (uk is null)
            return new UkInvoiceVat(null, []);

        var settings = (await taxes.GetAllAsync(cancellationToken)).Where(t => t.CustomerId == invoice.CustomerId).ToList();
        var vat = invoice.TaxLines
            .Where(t => !t.IsWithholding)
            .OrderBy(t => t.Order)
            .Select(t => (t, settings.FirstOrDefault(s => s.TaxId == t.TaxId)?
                .GetComplianceValues(UkComplianceModule.CountryCodeValue)
                .GetValueOrDefault(UkComplianceModule.TreatmentKey)))
            .ToList();
        return new UkInvoiceVat(uk.Settings, vat);
    }
}
