using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using nInvoices.Application.Localization;
using nInvoices.Application.Services.Holidays;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.Exceptions;

namespace nInvoices.Countries.Italy.FatturaPA;

/// <summary>
/// FatturaPA 1.2 in its FPR12 form: the XML invoice between businesses and to consumers that every Italian
/// invoice must be, sent through the Sistema di Interscambio (SDI). Unsigned, as FPR12 allows; the user, or
/// their intermediary, hands the file to the SDI. Invoices to public administrations (FPA12) are not built.
/// </summary>
public sealed class FatturaPaFormat : IEInvoiceFormat
{
    public const string Id = "fatturapa-1.2-fpr12";

    private const decimal Tolerance = 0.01m;

    private readonly ItalyComplianceModule _italy;
    private readonly IAppText _text;

    public FatturaPaFormat(ItalyComplianceModule italy, IAppText text)
    {
        _italy = italy;
        _text = text;
    }

    public string FormatId => Id;

    public string CountryCode => ItalyComplianceModule.CountryCodeValue;

    public string DisplayName => "FatturaPA (FPR12)";

    public bool RequiresSignature => false;

    // Every invoice an Italian business issues is an e-invoice, whoever the customer is
    public bool IsMandatoryFor(EInvoiceParty buyer) => true;

    public IReadOnlyList<ComplianceIssue> Validate(EInvoiceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = new List<ComplianceIssue>();

        foreach (var issue in _italy.ValidateIssuer(document.Issuer))
            issues.Add(issue with { Message = _text.Translate("compliance.einvoice.issuer", ("message", issue.Message)) });
        if (document.Issuer.Address is { } address && CountryCodes.FromName(address.Country) != ItalyComplianceModule.CountryCodeValue)
            issues.Add(new ComplianceIssue("address", _text.Translate("compliance.fatturapa.issuerInItaly")));

        ValidateBuyer(document.Buyer, issues);
        ValidateLinesAndTaxes(document, issues);

        if (string.IsNullOrWhiteSpace(document.Number))
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.noNumber")));
        else if (document.Number.Length > 20)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.numberTooLong", ("format", "FatturaPA"), ("max", 20))));

        if (document.Currency.Length != 3)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.currency")));

        return issues;
    }

    public EInvoiceArtifact Build(EInvoiceDocument document, X509Certificate2? signingCertificate)
    {
        var issues = Validate(document);
        if (issues.Count > 0)
            throw new DomainException(_text.Translate("errors.einvoice.notValid", ("format", "FatturaPA"), ("issues", string.Join("; ", issues.Select(i => i.Message)))));

        var xml = FatturaPaXmlBuilder.Build(document);

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
            xml.Save(writer);

        return new EInvoiceArtifact(stream.ToArray(), "application/xml", "xml");
    }

    private void ValidateBuyer(EInvoiceParty buyer, List<ComplianceIssue> issues)
    {
        var country = CountryCodes.FromName(buyer.Address.Country);

        if (string.IsNullOrWhiteSpace(buyer.Name))
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.customerNameRequired")));
        else if (buyer.Name.Length > 80)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.customerNameTooLong", ("max", 80))));

        if (country is null)
        {
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.customerCountryUnknown", ("country", buyer.Address.Country))));
        }
        else if (country == ItalyComplianceModule.CountryCodeValue)
        {
            var hasVat = ItalianTaxId.IsValidPartitaIva(buyer.TaxId);
            var hasFiscalCode = ItalianTaxId.IsValidCodiceFiscale(FatturaPaXmlBuilder.BuyerFiscalCode(buyer));
            if (!hasVat && !hasFiscalCode)
                issues.Add(new ComplianceIssue(null, _text.Translate("compliance.fatturapa.italianCustomerTaxId")));
            issues.AddRange(_italy.ValidateItalianAddress(buyer.Address, null, _text.Translate("compliance.common.subjectCustomerAddress")));
        }

        foreach (var issue in _italy.ValidateCustomer(new CustomerProfile(buyer.Name, buyer.TaxId, buyer.Address, buyer.Values)))
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.customer", ("message", issue.Message))));
    }

    private void ValidateLinesAndTaxes(EInvoiceDocument document, List<ComplianceIssue> issues)
    {
        if (document.Lines.Count == 0)
        {
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.noLines")));
            return;
        }

        foreach (var line in document.Lines)
        {
            if (line.Quantity <= 0)
                issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.lineQuantity", ("line", line.Description))));
            if (line.Amount < 0)
                issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.lineAmount", ("line", line.Description))));
        }

        var linesAmount = document.Lines.Sum(l => FatturaPaXmlBuilder.Round2(l.Amount));
        var taxes = ItalianInvoiceTaxes.Of(document.Taxes, linesAmount, document.Issuer.Values);

        if (taxes.OtherIva.Count > 0)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.fatturapa.singleIva")));

        if (taxes.Iva is null && !ItalyComplianceModule.ChargesNoIva(document.Issuer.Values))
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.fatturapa.ivaRequired")));
        else if (taxes.IvaRate == 0 && taxes.Nature is null)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.fatturapa.zeroIvaNature", ("tax", taxes.Iva?.Description))));

        if (taxes.Iva is { } iva)
        {
            if (Math.Abs(iva.TaxableBase - taxes.TaxableAmount) > Tolerance)
                issues.Add(new ComplianceIssue(null, _text.Translate(
                    taxes.Contributions.Count > 0 ? "compliance.fatturapa.ivaBaseWithContribution" : "compliance.fatturapa.ivaBase",
                    ("tax", iva.Description))));
            else if (Math.Abs(iva.Amount - iva.TaxableBase * iva.Rate / 100m) > Tolerance)
                issues.Add(new ComplianceIssue(null, _text.Translate("compliance.fatturapa.ivaAmount", ("tax", iva.Description))));
        }

        foreach (var contribution in taxes.Contributions)
        {
            if (Math.Abs(contribution.TaxableBase - linesAmount) > Tolerance)
                issues.Add(new ComplianceIssue(null, _text.Translate("compliance.fatturapa.contributionBase", ("tax", contribution.Description))));
        }

        var payable = taxes.TaxableAmount + (taxes.Iva?.Amount ?? 0m) - taxes.Withholdings.Sum(w => w.Amount);
        if (Math.Abs(payable - document.Total) > Tolerance)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.totalsMismatch")));
    }
}
