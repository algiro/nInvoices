using System.Globalization;
using System.Xml.Linq;
using nInvoices.Application.Services.Holidays;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Countries.Germany.XRechnung;

/// <summary>
/// Writes an XRechnung 3.0 invoice in UBL 2.1. Elements follow the UBL schema's order, which the schema
/// enforces; the business terms (BT-n) are EN 16931's.
/// </summary>
public static class XRechnungXmlBuilder
{
    public static readonly XNamespace Ubl = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    public const string CustomizationId = "urn:cen.eu:en16931:2017#compliant#urn:xeinkauf.de:kosit:xrechnung_3.0";
    public const string ProfileId = "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0";

    /// <summary>SEPA credit transfer (UNCL 4461).</summary>
    public const string SepaCreditTransfer = "58";

    public static XDocument Build(EInvoiceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var issuer = document.Issuer;
        var values = issuer.Values;
        var vat = GermanVat.Of(document.Taxes, values);
        var currency = document.Currency;
        var lines = document.Lines.Sum(l => Round2(l.Amount));
        // Not subject to VAT (O): no VAT identifier may appear (BR-O-2 to BR-O-4)
        var withVatIds = vat.Category != GermanyComplianceModule.NotSubject;

        var notes = new List<string>();
        if (GermanyComplianceModule.IsSmallBusiness(values))
            notes.Add(GermanyComplianceModule.SmallBusinessNote);
        if (vat.Category == GermanyComplianceModule.ReverseCharge)
            notes.Add(GermanyComplianceModule.ReverseChargeNote);
        if (!string.IsNullOrWhiteSpace(document.Notes))
            notes.Add(document.Notes.Trim());

        var buyerReference = document.Buyer.Values.GetValueOrDefault(GermanyComplianceModule.BuyerReferenceKey);

        var invoice = new XElement(Ubl + "Invoice",
            new XAttribute(XNamespace.Xmlns + "cac", Cac),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc),
            new XElement(Cbc + "CustomizationID", CustomizationId),
            new XElement(Cbc + "ProfileID", ProfileId),
            new XElement(Cbc + "ID", document.Number),
            new XElement(Cbc + "IssueDate", Date(document.IssueDate)),
            document.DueDate is { } due ? new XElement(Cbc + "DueDate", Date(due)) : null,
            new XElement(Cbc + "InvoiceTypeCode", "380"),
            notes.Select(n => new XElement(Cbc + "Note", n)),
            new XElement(Cbc + "DocumentCurrencyCode", currency),
            // BT-10, required by XRechnung: the Leitweg-ID or the customer's reference
            new XElement(Cbc + "BuyerReference", string.IsNullOrWhiteSpace(buyerReference) ? document.Number : buyerReference.Trim()),
            Supplier(issuer, withVatIds),
            Customer(document.Buyer, withVatIds),
            Payment(values, document.DueDate),
            new XElement(Cac + "TaxTotal",
                Money("TaxAmount", vat.Amount, currency),
                new XElement(Cac + "TaxSubtotal",
                    Money("TaxableAmount", lines, currency),
                    Money("TaxAmount", vat.Amount, currency),
                    TaxCategory("TaxCategory", vat, withReason: true))),
            new XElement(Cac + "LegalMonetaryTotal",
                Money("LineExtensionAmount", lines, currency),
                Money("TaxExclusiveAmount", lines, currency),
                Money("TaxInclusiveAmount", lines + vat.Amount, currency),
                Money("PayableAmount", document.Total, currency)),
            document.Lines.Select((line, index) => new XElement(Cac + "InvoiceLine",
                new XElement(Cbc + "ID", index + 1),
                new XElement(Cbc + "InvoicedQuantity", new XAttribute("unitCode", "C62"), Precise(line.Quantity)),
                Money("LineExtensionAmount", line.Amount, currency),
                new XElement(Cac + "Item",
                    new XElement(Cbc + "Name", Cut(line.Description, 1000)),
                    TaxCategory("ClassifiedTaxCategory", vat, withReason: false)),
                new XElement(Cac + "Price",
                    new XElement(Cbc + "PriceAmount", new XAttribute("currencyID", currency), Precise(line.UnitPrice))))));

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), invoice);
    }

    /// <summary>The buyer's VAT identifier: their Tax ID when it looks like one (two letters, then the number); else null.</summary>
    public static string? BuyerVatId(EInvoiceParty buyer)
    {
        ArgumentNullException.ThrowIfNull(buyer);
        var id = GermanTaxId.Normalize(buyer.TaxId);
        if (id.Length < 8 || !char.IsAsciiLetter(id[0]) || !char.IsAsciiLetter(id[1]))
            return null;
        return id.StartsWith(GermanyComplianceModule.CountryCodeValue, StringComparison.Ordinal) && !GermanTaxId.IsValidVatId(id) ? null : id;
    }

    private static XElement Supplier(IssuerProfile issuer, bool withVatIds)
    {
        var values = issuer.Values;
        var vatId = withVatIds ? GermanyComplianceModule.IssuerVatId(issuer.TaxId, values) : null;
        var taxNumber = GermanyComplianceModule.IssuerTaxNumber(issuer.TaxId);
        var email = values.GetValueOrDefault(GermanyComplianceModule.ContactEmailKey)?.Trim();

        return new XElement(Cac + "AccountingSupplierParty",
            new XElement(Cac + "Party",
                new XElement(Cbc + "EndpointID", new XAttribute("schemeID", "EM"), email),
                PostalAddress(issuer.Address!),
                vatId is null ? null : PartyTaxScheme(vatId, "VAT"),
                // The Steuernummer (BT-32): the "FC" scheme, for fiscal code
                taxNumber is null ? null : PartyTaxScheme(taxNumber, "FC"),
                new XElement(Cac + "PartyLegalEntity", new XElement(Cbc + "RegistrationName", Cut(issuer.LegalName, 200))),
                new XElement(Cac + "Contact",
                    new XElement(Cbc + "Name", values.GetValueOrDefault(GermanyComplianceModule.ContactNameKey)?.Trim()),
                    new XElement(Cbc + "Telephone", values.GetValueOrDefault(GermanyComplianceModule.ContactPhoneKey)?.Trim()),
                    new XElement(Cbc + "ElectronicMail", email))));
    }

    private static XElement Customer(EInvoiceParty buyer, bool withVatIds)
    {
        var vatId = withVatIds ? BuyerVatId(buyer) : null;
        return new XElement(Cac + "AccountingCustomerParty",
            new XElement(Cac + "Party",
                new XElement(Cbc + "EndpointID", new XAttribute("schemeID", "EM"),
                    buyer.Values.GetValueOrDefault(GermanyComplianceModule.BuyerEmailKey)?.Trim()),
                PostalAddress(buyer.Address),
                vatId is null ? null : PartyTaxScheme(vatId, "VAT"),
                new XElement(Cac + "PartyLegalEntity", new XElement(Cbc + "RegistrationName", Cut(buyer.Name, 200)))));
    }

    // How to pay (BG-16) and the payment terms (BT-20): two siblings, in this order
    private static XElement[] Payment(IReadOnlyDictionary<string, string> values, DateOnly? due)
    {
        var bic = values.GetValueOrDefault(GermanyComplianceModule.BicKey)?.Trim();
        return
        [
            new XElement(Cac + "PaymentMeans",
                new XElement(Cbc + "PaymentMeansCode", SepaCreditTransfer),
                new XElement(Cac + "PayeeFinancialAccount",
                    new XElement(Cbc + "ID", GermanTaxId.Normalize(values.GetValueOrDefault(GermanyComplianceModule.IbanKey))),
                    string.IsNullOrEmpty(bic) ? null : new XElement(Cac + "FinancialInstitutionBranch", new XElement(Cbc + "ID", bic.ToUpperInvariant())))),
            new XElement(Cac + "PaymentTerms", new XElement(Cbc + "Note",
                due is { } date ? $"Zahlbar bis {date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)} ohne Abzug." : "Zahlbar sofort ohne Abzug."))
        ];
    }

    private static XElement PostalAddress(Address address) => new(Cac + "PostalAddress",
        new XElement(Cbc + "StreetName", Cut($"{address.Street} {address.HouseNumber}".Trim(), 200)),
        new XElement(Cbc + "CityName", Cut(address.City, 100)),
        new XElement(Cbc + "PostalZone", address.ZipCode.Trim()),
        new XElement(Cac + "Country", new XElement(Cbc + "IdentificationCode",
            CountryCodes.FromName(address.Country) ?? GermanyComplianceModule.CountryCodeValue)));

    private static XElement PartyTaxScheme(string id, string scheme) => new(Cac + "PartyTaxScheme",
        new XElement(Cbc + "CompanyID", id),
        new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", scheme)));

    private static XElement TaxCategory(string name, GermanVat vat, bool withReason) => new(Cac + name,
        new XElement(Cbc + "ID", vat.Category),
        // Not subject (O) has no rate at all (BR-O-5, BR-O-10)
        vat.Category == GermanyComplianceModule.NotSubject ? null : new XElement(Cbc + "Percent", Amount(vat.Rate)),
        withReason && vat.ExemptionCode is not null ? new XElement(Cbc + "TaxExemptionReasonCode", vat.ExemptionCode) : null,
        withReason && vat.ExemptionReason is not null ? new XElement(Cbc + "TaxExemptionReason", vat.ExemptionReason) : null,
        new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT")));

    private static XElement Money(string name, decimal value, string currency) =>
        new(Cbc + name, new XAttribute("currencyID", currency), Amount(value));

    private static string Cut(string? text, int max)
    {
        var value = (text ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..max];
    }

    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Amount(decimal value) => Round2(value).ToString("0.00", CultureInfo.InvariantCulture);

    private static string Precise(decimal value) => value.ToString("0.00######", CultureInfo.InvariantCulture);

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
