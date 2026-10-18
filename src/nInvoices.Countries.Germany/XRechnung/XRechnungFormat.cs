using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using nInvoices.Application.Localization;
using nInvoices.Application.Services.Holidays;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.Exceptions;

namespace nInvoices.Countries.Germany.XRechnung;

/// <summary>
/// XRechnung 3.0 in its UBL 2.1 syntax: the German profile of the European e-invoice (EN 16931), which public
/// administrations require (with a Leitweg-ID) and every business must be able to receive. Unsigned. The
/// KoSIT validator's Schematron rules are followed for what this app can express, and checked here.
/// </summary>
public sealed class XRechnungFormat : IEInvoiceFormat
{
    public const string Id = "xrechnung-3.0-ubl";

    private const decimal Tolerance = 0.01m;

    private readonly GermanyComplianceModule _germany;
    private readonly IAppText _text;

    public XRechnungFormat(GermanyComplianceModule germany, IAppText text)
    {
        _germany = germany;
        _text = text;
    }

    public string FormatId => Id;

    public string CountryCode => GermanyComplianceModule.CountryCodeValue;

    public string DisplayName => "XRechnung (UBL)";

    public bool RequiresSignature => false;

    // Issuing it becomes mandatory between businesses in 2027 or 2028 (by revenue); generated on request until then
    public bool IsMandatoryFor(EInvoiceParty buyer) => false;

    public IReadOnlyList<ComplianceIssue> Validate(EInvoiceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = new List<ComplianceIssue>();
        var values = document.Issuer.Values;

        foreach (var issue in _germany.ValidateIssuer(document.Issuer))
            issues.Add(issue with { Message = _text.Translate("compliance.einvoice.issuer", ("message", issue.Message)) });
        if (document.Issuer.Address is { } address && CountryCodes.FromName(address.Country) != GermanyComplianceModule.CountryCodeValue)
            issues.Add(new ComplianceIssue("address", _text.Translate("compliance.xrechnung.issuerInGermany")));

        // XRechnung's own rules (BR-DE-2, BR-DE-1): a contact, and how to pay
        foreach (var (key, message) in new[]
        {
            (GermanyComplianceModule.ContactNameKey, "compliance.xrechnung.contactName"),
            (GermanyComplianceModule.ContactPhoneKey, "compliance.xrechnung.contactPhone"),
            (GermanyComplianceModule.ContactEmailKey, "compliance.xrechnung.contactEmail")
        })
        {
            if (string.IsNullOrWhiteSpace(values.GetValueOrDefault(key)))
                issues.Add(new ComplianceIssue(key, _text.Translate(message)));
        }
        if (string.IsNullOrWhiteSpace(values.GetValueOrDefault(GermanyComplianceModule.IbanKey)))
            issues.Add(new ComplianceIssue(GermanyComplianceModule.IbanKey, _text.Translate("compliance.xrechnung.ibanRequired")));

        ValidateBuyer(document.Buyer, issues);
        ValidateLinesAndTaxes(document, issues);

        if (string.IsNullOrWhiteSpace(document.Number))
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.noNumber")));
        if (document.Currency.Length != 3)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.currency")));

        return issues;
    }

    public EInvoiceArtifact Build(EInvoiceDocument document, X509Certificate2? signingCertificate)
    {
        var issues = Validate(document);
        if (issues.Count > 0)
            throw new DomainException(_text.Translate("errors.einvoice.notValid", ("format", "XRechnung"), ("issues", string.Join("; ", issues.Select(i => i.Message)))));

        var xml = XRechnungXmlBuilder.Build(document);

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
            xml.Save(writer);

        return new EInvoiceArtifact(stream.ToArray(), "application/xml", "xml");
    }

    private void ValidateBuyer(EInvoiceParty buyer, List<ComplianceIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(buyer.Name))
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.customerNameRequired")));
        if (CountryCodes.FromName(buyer.Address.Country) is null)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.customerCountryUnknown", ("country", buyer.Address.Country))));
        if (string.IsNullOrWhiteSpace(buyer.Address.City) || string.IsNullOrWhiteSpace(buyer.Address.ZipCode))
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.xrechnung.customerAddressRequired")));
        if (string.IsNullOrWhiteSpace(buyer.Values.GetValueOrDefault(GermanyComplianceModule.BuyerEmailKey)))
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.xrechnung.customerEmailRequired")));

        foreach (var issue in _germany.ValidateCustomer(new CustomerProfile(buyer.Name, buyer.TaxId, buyer.Address, buyer.Values)))
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

        var values = document.Issuer.Values;
        var vat = GermanVat.Of(document.Taxes, values);
        var lines = document.Lines.Sum(l => XRechnungXmlBuilder.Round2(l.Amount));
        var isSmallBusiness = GermanyComplianceModule.IsSmallBusiness(values);
        var sellerVatId = GermanyComplianceModule.IssuerVatId(document.Issuer.TaxId, values);
        var buyerHasVatId = XRechnungXmlBuilder.BuyerVatId(document.Buyer) is not null;

        if (vat.Withholdings.Count > 0)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.xrechnung.noWithholding")));
        if (vat.OtherVat.Count > 0)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.xrechnung.singleVat")));

        if (isSmallBusiness && vat.Rate > 0)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.xrechnung.smallBusinessVat")));
        else if (vat.Vat is null && !isSmallBusiness)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.xrechnung.vatRequired")));
        else if (vat.Category is null)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.xrechnung.zeroVatReason", ("tax", vat.Vat?.Description))));
        else if (vat.Category == GermanyComplianceModule.Exempt && vat.ExemptionReason is null)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.xrechnung.exemptReason", ("tax", vat.Vat?.Description))));

        // EN 16931: who must, or must not, carry a VAT identifier in each category
        switch (vat.Category)
        {
            case GermanyComplianceModule.ReverseCharge when !buyerHasVatId:
                issues.Add(new ComplianceIssue(null, _text.Translate("compliance.xrechnung.reverseChargeBuyerVatId")));
                break;
            case GermanyComplianceModule.IntraCommunity when sellerVatId is null || !buyerHasVatId:
                issues.Add(new ComplianceIssue(null, _text.Translate("compliance.xrechnung.intraCommunityVatIds")));
                break;
            case GermanyComplianceModule.Export when sellerVatId is null:
                issues.Add(new ComplianceIssue(null, _text.Translate("compliance.xrechnung.exportVatId")));
                break;
            // The USt-IdNr can't appear (BR-O-2), so the Steuernummer has to identify the seller (BR-CO-26)
            case GermanyComplianceModule.NotSubject when GermanyComplianceModule.IssuerTaxNumber(document.Issuer.TaxId) is null:
                issues.Add(new ComplianceIssue("taxId", _text.Translate("compliance.xrechnung.notSubjectTaxNumber")));
                break;
        }

        if (vat.Vat is { } tax)
        {
            if (Math.Abs(tax.TaxableBase - lines) > Tolerance)
                issues.Add(new ComplianceIssue(null, _text.Translate("compliance.xrechnung.vatBase", ("tax", tax.Description))));
            else if (Math.Abs(tax.Amount - tax.TaxableBase * tax.Rate / 100m) > Tolerance)
                issues.Add(new ComplianceIssue(null, _text.Translate("compliance.xrechnung.vatAmount", ("tax", tax.Description))));
        }

        if (vat.Withholdings.Count == 0 && Math.Abs(lines + vat.Amount - document.Total) > Tolerance)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.xrechnung.totalsMismatch")));
    }
}
