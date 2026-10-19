using nInvoices.Application.Compliance;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Countries.UnitedKingdom;

/// <summary>
/// What a template needs to print the United Kingdom's parts of an invoice, as <c>compliance.uk</c>: the VAT
/// number of a VAT-registered business, the trading name, and the note that explains a 0% VAT (zero-rated,
/// exempt, outside the scope, reverse charge). Always present: <c>isApplicable</c> is false without the UK rules.
/// </summary>
public sealed record UkTemplateModel
{
    public bool IsApplicable { get; init; }

    public bool IsVatRegistered { get; init; }

    /// <summary>"GB" and the nine digits, for a VAT-registered business; empty otherwise.</summary>
    public string VatNumber { get; init; } = string.Empty;

    public string TradingName { get; init; } = string.Empty;

    public bool HasVatNote { get; init; }

    public string VatNote { get; init; } = string.Empty;
}

public sealed class UkTemplateContributor : IInvoiceTemplateModelContributor
{
    public const string TemplateKey = "uk";

    private readonly IComplianceGate _gate;
    private readonly IRepository<Tax> _taxes;

    public UkTemplateContributor(IComplianceGate gate, IRepository<Tax> taxes)
    {
        _gate = gate;
        _taxes = taxes;
    }

    public string Key => TemplateKey;

    public object Empty { get; } = new UkTemplateModel();

    public object Sample { get; } = new UkTemplateModel
    {
        IsApplicable = true,
        IsVatRegistered = true,
        VatNumber = "GB 980 7806 84",
        TradingName = "Smith Consulting",
        HasVatNote = true,
        VatNote = UkComplianceModule.TreatmentNotes[UkComplianceModule.OutsideScope]
    };

    public async Task<object?> ContributeAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var vat = await UkInvoiceVat.OfAsync(invoice, _gate, _taxes, cancellationToken);
        if (vat.Issuer is not { } issuer)
            return null;

        var note = vat.Note;
        return new UkTemplateModel
        {
            IsApplicable = true,
            IsVatRegistered = vat.IsVatRegistered,
            VatNumber = vat.IsVatRegistered ? FormatVatNumber(issuer.TaxId) : string.Empty,
            TradingName = issuer.Values.GetValueOrDefault(UkComplianceModule.TradingNameKey) ?? string.Empty,
            HasVatNote = note is not null,
            VatNote = note ?? string.Empty
        };
    }

    /// <summary>"GB 123 4567 89", as HMRC writes it.</summary>
    public static string FormatVatNumber(string? vatNumber)
    {
        var id = UkTaxId.NormalizeVatNumber(vatNumber);
        return id.Length >= 9 ? $"GB {id[..3]} {id[3..7]} {id[7..9]}{(id.Length > 9 ? " " + id[9..] : string.Empty)}" : id;
    }
}
