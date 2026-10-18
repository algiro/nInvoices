using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
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

    public FatturaPaFormat(ItalyComplianceModule italy)
    {
        _italy = italy;
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
            issues.Add(issue with { Message = $"Issuer: {issue.Message}" });
        if (document.Issuer.Address is { } address && CountryCodes.FromName(address.Country) != ItalyComplianceModule.CountryCodeValue)
            issues.Add(new ComplianceIssue("address", "Issuer: the address must be in Italy"));

        ValidateBuyer(document.Buyer, issues);
        ValidateLinesAndTaxes(document, issues);

        if (string.IsNullOrWhiteSpace(document.Number))
            issues.Add(new ComplianceIssue(null, "The invoice has no number"));
        else if (document.Number.Length > 20)
            issues.Add(new ComplianceIssue(null, "FatturaPA invoice numbers have at most 20 characters; change the numbering pattern"));

        if (document.Currency.Length != 3)
            issues.Add(new ComplianceIssue(null, "The invoice currency must be a three-letter code"));

        return issues;
    }

    public EInvoiceArtifact Build(EInvoiceDocument document, X509Certificate2? signingCertificate)
    {
        var issues = Validate(document);
        if (issues.Count > 0)
            throw new DomainException("The invoice is not a valid FatturaPA: " + string.Join("; ", issues.Select(i => i.Message)));

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
            issues.Add(new ComplianceIssue(null, "Customer: the name is required"));
        else if (buyer.Name.Length > 80)
            issues.Add(new ComplianceIssue(null, "Customer: the name has at most 80 characters"));

        if (country is null)
        {
            issues.Add(new ComplianceIssue(null, $"Customer: the country \"{buyer.Address.Country}\" is not recognized"));
        }
        else if (country == ItalyComplianceModule.CountryCodeValue)
        {
            var hasVat = ItalianTaxId.IsValidPartitaIva(buyer.TaxId);
            var hasFiscalCode = ItalianTaxId.IsValidCodiceFiscale(FatturaPaXmlBuilder.BuyerFiscalCode(buyer));
            if (!hasVat && !hasFiscalCode)
                issues.Add(new ComplianceIssue(null, "Customer: an Italian customer needs a valid Partita IVA (the Tax ID) or codice fiscale"));
            issues.AddRange(ItalyComplianceModule.ValidateItalianAddress(buyer.Address, null, "Customer address"));
        }

        foreach (var issue in _italy.ValidateCustomer(new CustomerProfile(buyer.Name, buyer.TaxId, buyer.Address, buyer.Values)))
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

        var linesAmount = document.Lines.Sum(l => FatturaPaXmlBuilder.Round2(l.Amount));
        var taxes = ItalianInvoiceTaxes.Of(document.Taxes, linesAmount, document.Issuer.Values);

        if (taxes.OtherIva.Count > 0)
            issues.Add(new ComplianceIssue(null, "An Italian invoice has one IVA rate per line: keep a single IVA tax for this customer"));

        if (taxes.Iva is null && !ItalyComplianceModule.ChargesNoIva(document.Issuer.Values))
            issues.Add(new ComplianceIssue(null, "The invoice needs its IVA; if none is charged, add an IVA tax at 0% with the reason (natura)"));
        else if (taxes.IvaRate == 0 && taxes.Nature is null)
            issues.Add(new ComplianceIssue(null, $"IVA \"{taxes.Iva?.Description}\" is 0%: choose why no IVA is charged (natura) on the tax"));

        if (taxes.Iva is { } iva)
        {
            if (Math.Abs(iva.TaxableBase - taxes.TaxableAmount) > Tolerance)
                issues.Add(new ComplianceIssue(null, taxes.Contributions.Count > 0
                    ? $"IVA \"{iva.Description}\" must be calculated on the fee plus the contribution: tick \"Part of the taxable amount\" on the contribution and give it a lower order"
                    : $"IVA \"{iva.Description}\" is calculated on something other than the invoice total before taxes"));
            else if (Math.Abs(iva.Amount - iva.TaxableBase * iva.Rate / 100m) > Tolerance)
                issues.Add(new ComplianceIssue(null, $"IVA \"{iva.Description}\": the amount does not match the rate"));
        }

        foreach (var contribution in taxes.Contributions)
        {
            if (Math.Abs(contribution.TaxableBase - linesAmount) > Tolerance)
                issues.Add(new ComplianceIssue(null, $"Contribution \"{contribution.Description}\" must be calculated on the invoice lines"));
        }

        var payable = taxes.TaxableAmount + (taxes.Iva?.Amount ?? 0m) - taxes.Withholdings.Sum(w => w.Amount);
        if (Math.Abs(payable - document.Total) > Tolerance)
            issues.Add(new ComplianceIssue(null, "The lines and taxes do not add up to the invoice total"));
    }
}
