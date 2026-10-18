using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using nInvoices.Application.Localization;
using nInvoices.Application.Services.Holidays;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.Exceptions;

namespace nInvoices.Countries.Spain.Facturae;

/// <summary>
/// Facturae 3.2.2, the Spanish structured invoice, signed with XAdES-EPES. This is the format
/// public administrations require through FACe, and any business may use.
/// </summary>
public sealed class FacturaeFormat : IEInvoiceFormat
{
    public const string Id = "facturae-3.2.2";

    private const decimal Tolerance = 0.01m;

    private readonly SpainComplianceModule _spain;
    private readonly TimeProvider _time;
    private readonly IAppText _text;

    public FacturaeFormat(SpainComplianceModule spain, TimeProvider time, IAppText text)
    {
        _spain = spain;
        _time = time;
        _text = text;
    }

    public string FormatId => Id;

    public string CountryCode => SpainComplianceModule.CountryCodeValue;

    public string DisplayName => "Facturae 3.2.2";

    public bool RequiresSignature => true;

    // Public administrations only accept Facturae, through FACe; for everyone else it is voluntary
    public bool IsMandatoryFor(EInvoiceParty buyer) => SpainComplianceModule.IsPublicAdministration(buyer.Values);

    public IReadOnlyList<ComplianceIssue> Validate(EInvoiceDocument document)
    {
        var issues = new List<ComplianceIssue>();

        foreach (var issue in _spain.ValidateIssuer(document.Issuer))
            issues.Add(issue with { Message = _text.Translate("compliance.einvoice.issuer", ("message", issue.Message)) });
        if (document.Issuer.Address is { } issuerAddress && CountryCodes.FromName(issuerAddress.Country) != FacturaeCountries.Spain)
            issues.Add(new ComplianceIssue("address", _text.Translate("compliance.facturae.issuerInSpain")));
        ValidateIssuerNames(document, issues);

        ValidateBuyer(document.Buyer, issues);
        ValidateLinesAndTaxes(document, issues);

        if (string.IsNullOrWhiteSpace(document.Number))
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.noNumber")));
        else if (document.Number.Length > 20)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.numberTooLong", ("format", "Facturae"), ("max", 20))));

        if (document.Currency.Length != 3)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.currency")));

        return issues;
    }

    public EInvoiceArtifact Build(EInvoiceDocument document, X509Certificate2? signingCertificate)
    {
        var issues = Validate(document);
        if (issues.Count > 0)
            throw new DomainException(_text.Translate("errors.einvoice.notValid", ("format", "Facturae"), ("issues", string.Join("; ", issues.Select(i => i.Message)))));
        if (signingCertificate is null)
            throw new DomainException(_text.Translate("errors.facturae.certificateRequired"));

        var xml = FacturaeXmlBuilder.Build(document);

        var signed = new XmlDocument { PreserveWhitespace = true };
        using (var reader = xml.CreateReader())
            signed.Load(reader);

        XadesEpesSigner.Sign(signed, signingCertificate, _time.GetUtcNow().UtcDateTime);

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false }))
            signed.Save(writer);

        return new EInvoiceArtifact(stream.ToArray(), "application/xml", "xsig");
    }

    private void ValidateIssuerNames(EInvoiceDocument invoice, List<ComplianceIssue> issues)
    {
        var values = invoice.Issuer.Values;
        if (values.GetValueOrDefault(SpainComplianceModule.PersonTypeKey) == SpainComplianceModule.Individual)
        {
            foreach (var key in new[] { SpainComplianceModule.FirstNameKey, SpainComplianceModule.FirstSurnameKey, SpainComplianceModule.SecondSurnameKey })
            {
                if (values.GetValueOrDefault(key)?.Length > 40)
                    issues.Add(new ComplianceIssue(key, _text.Translate("compliance.facturae.issuerNameTooLong", ("max", 40))));
            }
        }
        else if (invoice.Issuer.LegalName?.Length > 80)
        {
            issues.Add(new ComplianceIssue("legalName", _text.Translate("compliance.facturae.issuerLegalNameTooLong", ("max", 80))));
        }
    }

    private void ValidateBuyer(EInvoiceParty buyer, List<ComplianceIssue> issues)
    {
        var country = CountryCodes.FromName(buyer.Address.Country);
        var isSpanish = country == FacturaeCountries.Spain;
        var isPublic = SpainComplianceModule.IsPublicAdministration(buyer.Values);
        var isIndividual = !isPublic && SpainComplianceModule.ResolvePersonType(buyer.TaxId, buyer.Values) == SpainComplianceModule.Individual;

        if (string.IsNullOrWhiteSpace(buyer.Name))
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.customerNameRequired")));
        else if (buyer.Name.Length > (isIndividual ? 40 : 80))
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.customerNameTooLong", ("max", isIndividual ? 40 : 80))));

        if (buyer.TaxId.Trim().Length < 3)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.facturae.customerTaxIdRequired")));
        else if (buyer.TaxId.Trim().Length > 30)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.facturae.customerTaxIdTooLong", ("max", 30))));
        else if (isSpanish && !SpanishTaxId.IsValid(buyer.TaxId))
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.facturae.customerNifInvalid")));

        if (FacturaeCountries.Alpha3(country) is null)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.customerCountryUnknown", ("country", buyer.Address.Country))));
        else if (isSpanish)
            issues.AddRange(_spain.ValidateSpanishAddress(buyer.Address, null!, _text.Translate("compliance.common.subjectCustomerAddress"))
                .Select(i => new ComplianceIssue(null, i.Message)));

        if (isPublic && !isSpanish)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.facturae.publicAdministrationInSpain")));

        foreach (var issue in _spain.ValidateCustomer(new CustomerProfile(buyer.Name, buyer.TaxId, buyer.Address, buyer.Values)))
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.customer", ("message", issue.Message))));
    }

    private void ValidateLinesAndTaxes(EInvoiceDocument invoice, List<ComplianceIssue> issues)
    {
        if (invoice.Lines.Count == 0)
        {
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.noLines")));
            return;
        }

        foreach (var line in invoice.Lines)
        {
            if (line.Quantity <= 0)
                issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.lineQuantity", ("line", line.Description))));
            if (line.Amount < 0)
                issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.lineAmount", ("line", line.Description))));
        }

        var gross = invoice.Lines.Sum(l => FacturaeXmlBuilder.Round2(l.Amount));
        var added = invoice.Taxes.Where(t => t.Kind == EInvoiceTaxKind.Added).ToList();
        var withheld = invoice.Taxes.Where(t => t.Kind == EInvoiceTaxKind.Withheld).ToList();

        if (added.Count == 0)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.facturae.taxRequired")));

        foreach (var tax in invoice.Taxes)
        {
            if (Math.Abs(tax.TaxableBase - gross) > Tolerance)
                issues.Add(new ComplianceIssue(null, _text.Translate("compliance.facturae.taxBase", ("tax", tax.Description))));
            else if (Math.Abs(tax.Amount - tax.TaxableBase * tax.Rate / 100m) > Tolerance)
                issues.Add(new ComplianceIssue(null, _text.Translate("compliance.facturae.taxAmount", ("tax", tax.Description))));
        }

        var payable = gross + added.Sum(t => t.Amount) - withheld.Sum(t => t.Amount);
        if (Math.Abs(payable - invoice.Total) > Tolerance)
            issues.Add(new ComplianceIssue(null, _text.Translate("compliance.einvoice.totalsMismatch")));
    }
}
