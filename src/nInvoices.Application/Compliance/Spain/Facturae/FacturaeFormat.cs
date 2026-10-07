using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using nInvoices.Application.Services.Holidays;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.Exceptions;

namespace nInvoices.Application.Compliance.Spain.Facturae;

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

    public FacturaeFormat(SpainComplianceModule spain, TimeProvider time)
    {
        _spain = spain;
        _time = time;
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
            issues.Add(issue with { Message = $"Issuer: {issue.Message}" });
        if (document.Issuer.Address is { } issuerAddress && CountryCodes.FromName(issuerAddress.Country) != FacturaeCountries.Spain)
            issues.Add(new ComplianceIssue("address", "Issuer: the address must be in Spain"));
        ValidateIssuerNames(document, issues);

        ValidateBuyer(document.Buyer, issues);
        ValidateLinesAndTaxes(document, issues);

        if (string.IsNullOrWhiteSpace(document.Number))
            issues.Add(new ComplianceIssue(null, "The invoice has no number"));
        else if (document.Number.Length > 20)
            issues.Add(new ComplianceIssue(null, "Facturae invoice numbers have at most 20 characters; change the numbering pattern"));

        if (document.Currency.Length != 3)
            issues.Add(new ComplianceIssue(null, "The invoice currency must be a three-letter code"));

        return issues;
    }

    public EInvoiceArtifact Build(EInvoiceDocument document, X509Certificate2? signingCertificate)
    {
        var issues = Validate(document);
        if (issues.Count > 0)
            throw new DomainException("The invoice is not valid Facturae: " + string.Join("; ", issues.Select(i => i.Message)));
        if (signingCertificate is null)
            throw new DomainException("Facturae invoices must be signed: upload a signing certificate in the Spanish compliance settings");

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

    private static void ValidateIssuerNames(EInvoiceDocument invoice, List<ComplianceIssue> issues)
    {
        var values = invoice.Issuer.Values;
        if (values.GetValueOrDefault(SpainComplianceModule.PersonTypeKey) == SpainComplianceModule.Individual)
        {
            foreach (var key in new[] { SpainComplianceModule.FirstNameKey, SpainComplianceModule.FirstSurnameKey, SpainComplianceModule.SecondSurnameKey })
            {
                if (values.GetValueOrDefault(key)?.Length > 40)
                    issues.Add(new ComplianceIssue(key, "Issuer: names have at most 40 characters"));
            }
        }
        else if (invoice.Issuer.LegalName?.Length > 80)
        {
            issues.Add(new ComplianceIssue("legalName", "Issuer: the legal name has at most 80 characters"));
        }
    }

    private void ValidateBuyer(EInvoiceParty buyer, List<ComplianceIssue> issues)
    {
        var country = CountryCodes.FromName(buyer.Address.Country);
        var isSpanish = country == FacturaeCountries.Spain;
        var isPublic = SpainComplianceModule.IsPublicAdministration(buyer.Values);
        var isIndividual = !isPublic && SpainComplianceModule.ResolvePersonType(buyer.TaxId, buyer.Values) == SpainComplianceModule.Individual;

        if (string.IsNullOrWhiteSpace(buyer.Name))
            issues.Add(new ComplianceIssue(null, "Customer: the name is required"));
        else if (buyer.Name.Length > (isIndividual ? 40 : 80))
            issues.Add(new ComplianceIssue(null, $"Customer: the name has at most {(isIndividual ? 40 : 80)} characters"));

        if (buyer.TaxId.Trim().Length < 3)
            issues.Add(new ComplianceIssue(null, "Customer: the tax id is required"));
        else if (buyer.TaxId.Trim().Length > 30)
            issues.Add(new ComplianceIssue(null, "Customer: the tax id has at most 30 characters"));
        else if (isSpanish && !SpanishTaxId.IsValid(buyer.TaxId))
            issues.Add(new ComplianceIssue(null, "Customer: this is not a valid Spanish NIF, NIE or CIF"));

        if (FacturaeCountries.Alpha3(country) is null)
            issues.Add(new ComplianceIssue(null, $"Customer: the country \"{buyer.Address.Country}\" is not recognized"));
        else if (isSpanish)
            issues.AddRange(SpainComplianceModule.ValidateSpanishAddress(buyer.Address, null!, "Customer address")
                .Select(i => new ComplianceIssue(null, i.Message)));

        if (isPublic && !isSpanish)
            issues.Add(new ComplianceIssue(null, "Customer: a public administration must have an address in Spain"));

        foreach (var issue in _spain.ValidateCustomer(new CustomerProfile(buyer.Name, buyer.TaxId, buyer.Address, buyer.Values)))
            issues.Add(new ComplianceIssue(null, $"Customer: {issue.Message}"));
    }

    private static void ValidateLinesAndTaxes(EInvoiceDocument invoice, List<ComplianceIssue> issues)
    {
        if (invoice.Lines.Count == 0)
        {
            issues.Add(new ComplianceIssue(null, "The invoice has no lines"));
            return;
        }

        foreach (var line in invoice.Lines)
        {
            if (line.Quantity <= 0)
                issues.Add(new ComplianceIssue(null, $"Line \"{line.Description}\": the quantity must be positive"));
            if (line.Amount < 0)
                issues.Add(new ComplianceIssue(null, $"Line \"{line.Description}\": the amount cannot be negative"));
        }

        var gross = invoice.Lines.Sum(l => FacturaeXmlBuilder.Round2(l.Amount));
        var added = invoice.Taxes.Where(t => t.Kind == EInvoiceTaxKind.Added).ToList();
        var withheld = invoice.Taxes.Where(t => t.Kind == EInvoiceTaxKind.Withheld).ToList();

        if (added.Count == 0)
            issues.Add(new ComplianceIssue(null, "The invoice needs at least one tax; for an exempt invoice add a tax at 0%"));

        foreach (var tax in invoice.Taxes)
        {
            if (Math.Abs(tax.TaxableBase - gross) > Tolerance)
                issues.Add(new ComplianceIssue(null, $"Tax \"{tax.Description}\" is calculated on something other than the invoice total before taxes, which Facturae cannot express"));
            else if (Math.Abs(tax.Amount - tax.TaxableBase * tax.Rate / 100m) > Tolerance)
                issues.Add(new ComplianceIssue(null, $"Tax \"{tax.Description}\": the amount does not match the rate"));
        }

        var payable = gross + added.Sum(t => t.Amount) - withheld.Sum(t => t.Amount);
        if (Math.Abs(payable - invoice.Total) > Tolerance)
            issues.Add(new ComplianceIssue(null, "The lines and taxes do not add up to the invoice total"));
    }
}
