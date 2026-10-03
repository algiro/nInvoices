using System.Globalization;
using System.Xml.Linq;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Compliance.Spain.Facturae;

/// <summary>
/// Builds the unsigned Facturae 3.2.2 document of an invoice. Elements follow the order of the
/// official schema; the caller has already checked the document with <see cref="FacturaeFormat.Validate"/>.
/// </summary>
internal static class FacturaeXmlBuilder
{
    public const string SchemaVersion = "3.2.2";
    public static readonly XNamespace Fe = "http://www.facturae.gob.es/formato/Versiones/Facturaev3_2_2.xml";
    public static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // Languages the schema accepts for the invoice text
    private static readonly HashSet<string> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        "ar", "be", "bg", "ca", "cs", "da", "de", "el", "en", "es", "et", "eu", "fi", "fr", "ga", "gl", "hr", "hu",
        "it", "lt", "lv", "mt", "nl", "no", "pl", "pt", "ro", "ru", "sk", "sl", "sv", "tr", "uk"
    };

    public static XDocument Build(EInvoiceDocument invoice)
    {
        var added = invoice.Taxes.Where(t => t.Kind == EInvoiceTaxKind.Added).ToList();
        var withheld = invoice.Taxes.Where(t => t.Kind == EInvoiceTaxKind.Withheld).ToList();

        var gross = invoice.Lines.Sum(l => Round2(l.Amount));
        var totalAdded = added.Sum(t => Round2(t.Amount));
        var totalWithheld = withheld.Sum(t => Round2(t.Amount));
        var invoiceTotal = gross + totalAdded;
        var outstanding = invoiceTotal - totalWithheld;

        var sellerId = SpanishTaxId.Normalize(invoice.Issuer.TaxId);

        var root = new XElement(Fe + "Facturae",
            new XAttribute(XNamespace.Xmlns + "fe", Fe.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "ds", Ds.NamespaceName),
            new XElement("FileHeader",
                new XElement("SchemaVersion", SchemaVersion),
                new XElement("Modality", "I"),
                new XElement("InvoiceIssuerType", "EM"),
                new XElement("Batch",
                    new XElement("BatchIdentifier", Truncate($"{sellerId}{invoice.Number}", 70)),
                    new XElement("InvoicesCount", 1),
                    Amount("TotalInvoicesAmount", invoiceTotal),
                    Amount("TotalOutstandingAmount", outstanding),
                    Amount("TotalExecutableAmount", outstanding),
                    new XElement("InvoiceCurrencyCode", invoice.Currency.ToUpperInvariant()))),
            new XElement("Parties",
                new XElement("SellerParty", SellerContent(invoice, sellerId)),
                new XElement("BuyerParty", BuyerContent(invoice.Buyer))),
            new XElement("Invoices",
                new XElement("Invoice",
                    new XElement("InvoiceHeader",
                        new XElement("InvoiceNumber", Truncate(invoice.Number, 20)),
                        new XElement("InvoiceDocumentType", "FC"),
                        new XElement("InvoiceClass", "OO")),
                    new XElement("InvoiceIssueData",
                        new XElement("IssueDate", invoice.IssueDate.ToString("yyyy-MM-dd", Invariant)),
                        new XElement("InvoiceCurrencyCode", invoice.Currency.ToUpperInvariant()),
                        new XElement("TaxCurrencyCode", invoice.Currency.ToUpperInvariant()),
                        new XElement("LanguageName", PickLanguage(invoice.Language))),
                    new XElement("TaxesOutputs", added.Select(t => new XElement("Tax",
                        new XElement("TaxTypeCode", "01"),
                        new XElement("TaxRate", Rate(t.Rate)),
                        Amount("TaxableBase", t.TaxableBase),
                        Amount("TaxAmount", t.Amount)))),
                    withheld.Count == 0
                        ? null
                        : new XElement("TaxesWithheld", withheld.Select(t => new XElement("Tax",
                            new XElement("TaxTypeCode", "04"),
                            new XElement("TaxRate", Rate(t.Rate)),
                            Amount("TaxableBase", t.TaxableBase),
                            Amount("TaxAmount", t.Amount)))),
                    new XElement("InvoiceTotals",
                        new XElement("TotalGrossAmount", Money2(gross)),
                        new XElement("TotalGrossAmountBeforeTaxes", Money2(gross)),
                        new XElement("TotalTaxOutputs", Money2(totalAdded)),
                        new XElement("TotalTaxesWithheld", Money2(totalWithheld)),
                        new XElement("InvoiceTotal", Money2(invoiceTotal)),
                        new XElement("TotalOutstandingAmount", Money2(outstanding)),
                        new XElement("TotalExecutableAmount", Money2(outstanding))),
                    new XElement("Items", Lines(invoice, added, withheld)),
                    invoice.Notes is { Length: > 0 }
                        ? new XElement("AdditionalData", new XElement("InvoiceAdditionalInformation", Truncate(Clean(invoice.Notes), 2500)))
                        : null)));

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
    }

    private static IEnumerable<XElement> Lines(EInvoiceDocument invoice, List<EInvoiceTax> added, List<EInvoiceTax> withheld)
    {
        // The tax of each line is its share of the invoice tax; the last line takes the rounding
        // difference, so the lines always add up to the invoice-level amount
        var addedShares = added.Select(t => Share(t, invoice.Lines)).ToList();
        var withheldShares = withheld.Select(t => Share(t, invoice.Lines)).ToList();

        for (var i = 0; i < invoice.Lines.Count; i++)
        {
            var line = invoice.Lines[i];
            var amount = Round2(line.Amount);
            var unitPrice = Round2(line.Quantity * line.UnitPrice) == amount
                ? line.UnitPrice
                : Math.Round(amount / line.Quantity, 8, MidpointRounding.AwayFromZero);

            yield return new XElement("InvoiceLine",
                new XElement("ItemDescription", Truncate(Clean(line.Description), 2500)),
                new XElement("Quantity", line.Quantity.ToString("0.######", Invariant)),
                new XElement("UnitOfMeasure", "01"),
                new XElement("UnitPriceWithoutTax", Up8(unitPrice)),
                new XElement("TotalCost", Money2(amount)),
                new XElement("GrossAmount", Money2(amount)),
                withheld.Count == 0
                    ? null
                    : new XElement("TaxesWithheld", withheld.Select((t, n) => new XElement("Tax",
                        new XElement("TaxTypeCode", "04"),
                        new XElement("TaxRate", Rate(t.Rate)),
                        Amount("TaxableBase", amount),
                        Amount("TaxAmount", withheldShares[n][i])))),
                new XElement("TaxesOutputs", added.Select((t, n) => new XElement("Tax",
                    new XElement("TaxTypeCode", "01"),
                    new XElement("TaxRate", Rate(t.Rate)),
                    Amount("TaxableBase", amount),
                    Amount("TaxAmount", addedShares[n][i])))));
        }
    }

    /// <summary>The tax of each line: rate applied to the line, the last one absorbing the rounding difference.</summary>
    private static decimal[] Share(EInvoiceTax tax, IReadOnlyList<EInvoiceLine> lines)
    {
        var shares = lines.Select(l => Round2(Round2(l.Amount) * tax.Rate / 100m)).ToArray();
        if (shares.Length > 0)
            shares[^1] += Round2(tax.Amount) - shares.Sum();
        return shares;
    }

    private static IEnumerable<object> SellerContent(EInvoiceDocument invoice, string taxId)
    {
        var issuer = invoice.Issuer;
        var isIndividual = issuer.Values.GetValueOrDefault(SpainComplianceModule.PersonTypeKey) == SpainComplianceModule.Individual;

        yield return TaxIdentification(isIndividual, "R", taxId);

        if (isIndividual)
        {
            yield return new XElement("Individual",
                new XElement("Name", Truncate(Clean(issuer.Values[SpainComplianceModule.FirstNameKey]), 40)),
                new XElement("FirstSurname", Truncate(Clean(issuer.Values[SpainComplianceModule.FirstSurnameKey]), 40)),
                issuer.Values.TryGetValue(SpainComplianceModule.SecondSurnameKey, out var second)
                    ? new XElement("SecondSurname", Truncate(Clean(second), 40))
                    : null,
                AddressContent(issuer.Address!, FacturaeCountries.Spain));
        }
        else
        {
            yield return new XElement("LegalEntity",
                new XElement("CorporateName", Truncate(Clean(issuer.LegalName!), 80)),
                AddressContent(issuer.Address!, FacturaeCountries.Spain));
        }
    }

    private static IEnumerable<object> BuyerContent(EInvoiceParty buyer)
    {
        var country = Services.Holidays.CountryCodes.FromName(buyer.Address.Country);
        var isPublic = SpainComplianceModule.IsPublicAdministration(buyer.Values);
        var personType = SpainComplianceModule.ResolvePersonType(buyer.TaxId, buyer.Values);
        var isIndividual = personType == SpainComplianceModule.Individual && !isPublic;
        var taxId = country == FacturaeCountries.Spain ? SpanishTaxId.Normalize(buyer.TaxId) : buyer.TaxId.Trim().ToUpperInvariant();

        yield return TaxIdentification(isIndividual, FacturaeCountries.ResidenceTypeCode(country), taxId);

        if (isPublic)
        {
            // FACe routes by these three DIR3 codes
            yield return new XElement("AdministrativeCentres",
                Centre(buyer, SpainComplianceModule.AccountingOfficeKey, "01"),
                Centre(buyer, SpainComplianceModule.ManagingBodyKey, "02"),
                Centre(buyer, SpainComplianceModule.ProcessingUnitKey, "03"));
        }

        if (isIndividual)
        {
            yield return new XElement("Individual",
                new XElement("Name", Truncate(Clean(buyer.Name), 40)),
                new XElement("FirstSurname", Truncate(Clean(buyer.Values[SpainComplianceModule.FirstSurnameKey]), 40)),
                buyer.Values.TryGetValue(SpainComplianceModule.SecondSurnameKey, out var second)
                    ? new XElement("SecondSurname", Truncate(Clean(second), 40))
                    : null,
                AddressContent(buyer.Address, country));
        }
        else
        {
            yield return new XElement("LegalEntity",
                new XElement("CorporateName", Truncate(Clean(buyer.Name), 80)),
                AddressContent(buyer.Address, country));
        }
    }

    private static XElement Centre(EInvoiceParty buyer, string codeKey, string role) =>
        new("AdministrativeCentre",
            new XElement("CentreCode", buyer.Values[codeKey].Trim()),
            new XElement("RoleTypeCode", role),
            new XElement("Name", Truncate(Clean(buyer.Name), 40)),
            AddressContent(buyer.Address, FacturaeCountries.Spain));

    private static XElement TaxIdentification(bool individual, string residence, string taxId) =>
        new("TaxIdentification",
            new XElement("PersonTypeCode", individual ? "F" : "J"),
            new XElement("ResidenceTypeCode", residence),
            new XElement("TaxIdentificationNumber", Truncate(taxId, 30)));

    private static XElement AddressContent(Address address, string? countryAlpha2)
    {
        var street = Truncate(Clean($"{address.Street} {address.HouseNumber}".Trim()), 80);
        var alpha3 = FacturaeCountries.Alpha3(countryAlpha2)!;

        if (string.Equals(countryAlpha2, FacturaeCountries.Spain, StringComparison.OrdinalIgnoreCase))
        {
            return new XElement("AddressInSpain",
                new XElement("Address", street),
                new XElement("PostCode", address.ZipCode.Trim()),
                new XElement("Town", Truncate(Clean(address.City), 50)),
                new XElement("Province", Truncate(Clean(address.State ?? ""), 20)),
                new XElement("CountryCode", alpha3));
        }

        return new XElement("OverseasAddress",
            new XElement("Address", street),
            new XElement("PostCodeAndTown", Truncate(Clean($"{address.ZipCode} {address.City}".Trim()), 50)),
            new XElement("Province", Truncate(Clean(string.IsNullOrWhiteSpace(address.State) ? address.City : address.State), 20)),
            new XElement("CountryCode", alpha3));
    }

    private static string PickLanguage(string? language)
    {
        var code = language?.Split('-', '_')[0].ToLowerInvariant();
        return code is not null && Languages.Contains(code) ? code : "es";
    }

    private static XElement Amount(string name, decimal value) =>
        new(name, new XElement("TotalAmount", Money2(value)));

    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Money2(decimal value) => Round2(value).ToString("0.00", Invariant);

    private static string Up8(decimal value) => Math.Round(value, 8, MidpointRounding.AwayFromZero).ToString("0.00######", Invariant);

    private static string Rate(decimal value) => Math.Round(value, 8, MidpointRounding.AwayFromZero).ToString("0.00######", Invariant);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    /// <summary>Drops the control characters XML cannot carry.</summary>
    private static string Clean(string value) =>
        new(value.Where(c => c is '\t' or '\n' or '\r' || c >= ' ').ToArray());
}
