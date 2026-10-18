using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
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

    public XRechnungFormat(GermanyComplianceModule germany)
    {
        _germany = germany;
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
            issues.Add(issue with { Message = $"Issuer: {issue.Message}" });
        if (document.Issuer.Address is { } address && CountryCodes.FromName(address.Country) != GermanyComplianceModule.CountryCodeValue)
            issues.Add(new ComplianceIssue("address", "Issuer: the address must be in Germany"));

        // XRechnung's own rules (BR-DE-2, BR-DE-1): a contact, and how to pay
        foreach (var (key, label) in new[]
        {
            (GermanyComplianceModule.ContactNameKey, "contact name"),
            (GermanyComplianceModule.ContactPhoneKey, "contact phone"),
            (GermanyComplianceModule.ContactEmailKey, "contact email")
        })
        {
            if (string.IsNullOrWhiteSpace(values.GetValueOrDefault(key)))
                issues.Add(new ComplianceIssue(key, $"Issuer: XRechnung needs a {label}"));
        }
        if (string.IsNullOrWhiteSpace(values.GetValueOrDefault(GermanyComplianceModule.IbanKey)))
            issues.Add(new ComplianceIssue(GermanyComplianceModule.IbanKey, "Issuer: XRechnung needs the IBAN the customer pays to"));

        ValidateBuyer(document.Buyer, issues);
        ValidateLinesAndTaxes(document, issues);

        if (string.IsNullOrWhiteSpace(document.Number))
            issues.Add(new ComplianceIssue(null, "The invoice has no number"));
        if (document.Currency.Length != 3)
            issues.Add(new ComplianceIssue(null, "The invoice currency must be a three-letter code"));

        return issues;
    }

    public EInvoiceArtifact Build(EInvoiceDocument document, X509Certificate2? signingCertificate)
    {
        var issues = Validate(document);
        if (issues.Count > 0)
            throw new DomainException("The invoice is not a valid XRechnung: " + string.Join("; ", issues.Select(i => i.Message)));

        var xml = XRechnungXmlBuilder.Build(document);

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
            xml.Save(writer);

        return new EInvoiceArtifact(stream.ToArray(), "application/xml", "xml");
    }

    private void ValidateBuyer(EInvoiceParty buyer, List<ComplianceIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(buyer.Name))
            issues.Add(new ComplianceIssue(null, "Customer: the name is required"));
        if (CountryCodes.FromName(buyer.Address.Country) is null)
            issues.Add(new ComplianceIssue(null, $"Customer: the country \"{buyer.Address.Country}\" is not recognized"));
        if (string.IsNullOrWhiteSpace(buyer.Address.City) || string.IsNullOrWhiteSpace(buyer.Address.ZipCode))
            issues.Add(new ComplianceIssue(null, "Customer: the city and postal code are required"));
        if (string.IsNullOrWhiteSpace(buyer.Values.GetValueOrDefault(GermanyComplianceModule.BuyerEmailKey)))
            issues.Add(new ComplianceIssue(null, "Customer: XRechnung needs the email address their e-invoices go to (customer › E-invoice email)"));

        foreach (var issue in _germany.ValidateCustomer(new CustomerProfile(buyer.Name, buyer.TaxId, buyer.Address, buyer.Values)))
            issues.Add(new ComplianceIssue(null, $"Customer: {issue.Message}"));
    }

    private static void ValidateLinesAndTaxes(EInvoiceDocument document, List<ComplianceIssue> issues)
    {
        if (document.Lines.Count == 0)
        {
            issues.Add(new ComplianceIssue(null, "The invoice has no lines"));
            return;
        }

        foreach (var line in document.Lines)
        {
            if (line.Quantity <= 0)
                issues.Add(new ComplianceIssue(null, $"Line \"{line.Description}\": the quantity must be positive"));
            if (line.Amount < 0)
                issues.Add(new ComplianceIssue(null, $"Line \"{line.Description}\": the amount cannot be negative"));
        }

        var values = document.Issuer.Values;
        var vat = GermanVat.Of(document.Taxes, values);
        var lines = document.Lines.Sum(l => XRechnungXmlBuilder.Round2(l.Amount));
        var isSmallBusiness = GermanyComplianceModule.IsSmallBusiness(values);
        var sellerVatId = GermanyComplianceModule.IssuerVatId(document.Issuer.TaxId, values);
        var buyerHasVatId = XRechnungXmlBuilder.BuyerVatId(document.Buyer) is not null;

        if (vat.Withholdings.Count > 0)
            issues.Add(new ComplianceIssue(null, "XRechnung has no withholding: remove the negative tax from this customer"));
        if (vat.OtherVat.Count > 0)
            issues.Add(new ComplianceIssue(null, "One invoice has one VAT rate here: keep a single VAT tax for this customer"));

        if (isSmallBusiness && vat.Rate > 0)
            issues.Add(new ComplianceIssue(null, "A Kleinunternehmer charges no VAT: remove the VAT tax, or untick Kleinunternehmer"));
        else if (vat.Vat is null && !isSmallBusiness)
            issues.Add(new ComplianceIssue(null, "The invoice needs its VAT; if none is charged, add a VAT tax at 0% and choose why"));
        else if (vat.Category is null)
            issues.Add(new ComplianceIssue(null, $"VAT \"{vat.Vat?.Description}\" is 0%: choose why no VAT is charged on the tax"));
        else if (vat.Category == GermanyComplianceModule.Exempt && vat.ExemptionReason is null)
            issues.Add(new ComplianceIssue(null, $"VAT \"{vat.Vat?.Description}\" is exempt: give the reason on the tax"));

        // EN 16931: who must, or must not, carry a VAT identifier in each category
        switch (vat.Category)
        {
            case GermanyComplianceModule.ReverseCharge when !buyerHasVatId:
                issues.Add(new ComplianceIssue(null, "Reverse charge: the customer's VAT identifier is required (the customer's Tax ID)"));
                break;
            case GermanyComplianceModule.IntraCommunity when sellerVatId is null || !buyerHasVatId:
                issues.Add(new ComplianceIssue(null, "An intra-community supply needs your USt-IdNr and the customer's VAT identifier"));
                break;
            case GermanyComplianceModule.Export when sellerVatId is null:
                issues.Add(new ComplianceIssue(null, "An export needs your USt-IdNr"));
                break;
            // The USt-IdNr can't appear (BR-O-2), so the Steuernummer has to identify the seller (BR-CO-26)
            case GermanyComplianceModule.NotSubject when GermanyComplianceModule.IssuerTaxNumber(document.Issuer.TaxId) is null:
                issues.Add(new ComplianceIssue("taxId", "Not subject to VAT: XRechnung then needs your Steuernummer as the Tax ID, since the USt-IdNr cannot appear"));
                break;
        }

        if (vat.Vat is { } tax)
        {
            if (Math.Abs(tax.TaxableBase - lines) > Tolerance)
                issues.Add(new ComplianceIssue(null, $"VAT \"{tax.Description}\" is calculated on something other than the invoice lines"));
            else if (Math.Abs(tax.Amount - tax.TaxableBase * tax.Rate / 100m) > Tolerance)
                issues.Add(new ComplianceIssue(null, $"VAT \"{tax.Description}\": the amount does not match the rate"));
        }

        if (vat.Withholdings.Count == 0 && Math.Abs(lines + vat.Amount - document.Total) > Tolerance)
            issues.Add(new ComplianceIssue(null, "The lines and VAT do not add up to the invoice total"));
    }
}
