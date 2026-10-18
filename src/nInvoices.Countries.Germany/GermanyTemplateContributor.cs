using nInvoices.Application.Compliance;
using nInvoices.Application.Services;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Countries.Germany.XRechnung;

namespace nInvoices.Countries.Germany;

/// <summary>
/// What a template needs to print the German parts of an invoice, as <c>compliance.germany</c>: the Steuernummer
/// or USt-IdNr (§14 UStG asks for one), and the note that explains a missing VAT (Kleinunternehmer, reverse
/// charge, an exemption). Always present: <c>isApplicable</c> is false for a user without the German rules.
/// </summary>
public sealed record GermanyTemplateModel
{
    public bool IsApplicable { get; init; }

    public bool IsSmallBusiness { get; init; }

    public string TaxNumber { get; init; } = string.Empty;

    public string VatId { get; init; } = string.Empty;

    /// <summary>True when the invoice charges no VAT and <see cref="VatNote"/> says why.</summary>
    public bool HasVatNote { get; init; }

    /// <summary>"Gemäß § 19 UStG wird keine Umsatzsteuer berechnet.", the reverse-charge note, an exemption's reason; or empty.</summary>
    public string VatNote { get; init; } = string.Empty;
}

public sealed class GermanyTemplateContributor : IInvoiceTemplateModelContributor
{
    public const string TemplateKey = "germany";

    private readonly IComplianceGate _gate;
    private readonly IRepository<Tax> _taxes;

    public GermanyTemplateContributor(IComplianceGate gate, IRepository<Tax> taxes)
    {
        _gate = gate;
        _taxes = taxes;
    }

    public string Key => TemplateKey;

    public object Empty { get; } = new GermanyTemplateModel();

    public object Sample { get; } = new GermanyTemplateModel
    {
        IsApplicable = true,
        IsSmallBusiness = true,
        TaxNumber = "21/815/08150",
        HasVatNote = true,
        VatNote = GermanyComplianceModule.SmallBusinessNote
    };

    public async Task<object?> ContributeAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var germany = (await _gate.GetActiveAsync(cancellationToken))
            .FirstOrDefault(a => a.Module.CountryCode == GermanyComplianceModule.CountryCodeValue);
        if (germany is null)
            return null;

        var issuer = germany.Settings.ToIssuerProfile();
        var settings = (await _taxes.GetAllAsync(cancellationToken)).Where(t => t.CustomerId == invoice.CustomerId).ToList();
        var taxes = invoice.TaxLines
            .OrderBy(t => t.Order)
            .Select(t => new EInvoiceTax(
                t.IsWithholding ? EInvoiceTaxKind.Withheld : EInvoiceTaxKind.Added,
                t.Description,
                Math.Abs(t.Rate),
                t.BaseAmount.Amount,
                Math.Abs(t.TaxAmount.Amount),
                settings.FirstOrDefault(s => s.TaxId == t.TaxId)?.GetComplianceValues(GermanyComplianceModule.CountryCodeValue)))
            .ToList();
        var vat = GermanVat.Of(taxes, issuer.Values);

        return new GermanyTemplateModel
        {
            IsApplicable = true,
            IsSmallBusiness = GermanyComplianceModule.IsSmallBusiness(issuer.Values),
            TaxNumber = GermanyComplianceModule.IssuerTaxNumber(issuer.TaxId) ?? string.Empty,
            VatId = GermanyComplianceModule.IssuerVatId(issuer.TaxId, issuer.Values) ?? string.Empty,
            HasVatNote = vat.ExemptionReason is not null,
            VatNote = vat.ExemptionReason ?? string.Empty
        };
    }
}
