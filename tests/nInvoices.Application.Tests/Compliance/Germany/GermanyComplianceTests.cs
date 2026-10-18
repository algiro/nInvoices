using System.Xml.Linq;
using Moq;
using nInvoices.Application.Compliance;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using nInvoices.Countries.Germany;
using nInvoices.Countries.Germany.XRechnung;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance.Germany;

internal static class XRechnungTestData
{
    public static IssuerProfile Issuer(bool smallBusiness = false, string taxId = "21/815/08150") => new(
        "Anna Schmidt IT-Beratung",
        taxId,
        new Address("Hauptstraße", "5", "Berlin", "10115", "Germany"),
        new Dictionary<string, string>
        {
            [GermanyComplianceModule.SmallBusinessKey] = smallBusiness ? "true" : "",
            [GermanyComplianceModule.VatIdKey] = "DE123456788",
            [GermanyComplianceModule.ContactNameKey] = "Anna Schmidt",
            [GermanyComplianceModule.ContactPhoneKey] = "+49 30 1234567",
            [GermanyComplianceModule.ContactEmailKey] = "anna@schmidt.de",
            [GermanyComplianceModule.IbanKey] = "DE89 3704 0044 0532 0130 00"
        });

    public static EInvoiceParty GermanCustomer(string email = "rechnung@kunde.de") => new(
        "Kunde GmbH",
        "DE123456788",
        new Address("Marktplatz", "1", "München", "80331", "Deutschland"),
        new Dictionary<string, string> { [GermanyComplianceModule.BuyerEmailKey] = email });

    public static EInvoiceParty FrenchCustomer() => new(
        "Client SARL",
        "FR40303265045",
        new Address("Rue de Rivoli", "10", "Paris", "75001", "France"),
        new Dictionary<string, string> { [GermanyComplianceModule.BuyerEmailKey] = "factures@client.fr" });

    public static EInvoiceTax Vat(decimal rate, decimal lines, IReadOnlyDictionary<string, string>? values = null) =>
        new(EInvoiceTaxKind.Added, $"USt {rate}%", rate, lines, Math.Round(lines * rate / 100m, 2), values);

    public static EInvoiceDocument Invoice(IssuerProfile issuer, EInvoiceParty buyer, IReadOnlyList<EInvoiceTax> taxes) => new(
        "2026-042",
        new DateOnly(2026, 10, 15),
        new DateOnly(2026, 11, 14),
        "EUR",
        issuer,
        buyer,
        [new EInvoiceLine("Beratung Oktober", 10m, 100m, 1000m)],
        taxes,
        1000m + taxes.Where(t => t.Kind == EInvoiceTaxKind.Added).Sum(t => t.Amount),
        "Vielen Dank für Ihren Auftrag.",
        "de");
}

[TestFixture]
public sealed class GermanTaxIdTests
{
    [TestCase("DE123456788", true)]
    [TestCase("de 123 456 788", true)]
    [TestCase("DE123456789", false)]
    [TestCase("DE12345678", false)]
    [TestCase("FR123456788", false)]
    public void IsValidVatId_ChecksTheCheckDigit(string id, bool valid) => GermanTaxId.IsValidVatId(id).ShouldBe(valid);

    [TestCase("21/815/08150", true)]
    [TestCase("181/815/08155", true)]
    [TestCase("2181508150", true)]
    [TestCase("1121081508150", true)]
    [TestCase("123/456", false)]
    [TestCase("21/815/0815A", false)]
    public void IsPlausibleTaxNumber_CountsTheDigits(string number, bool plausible) =>
        GermanTaxId.IsPlausibleTaxNumber(number).ShouldBe(plausible);

    [TestCase("DE89 3704 0044 0532 0130 00", true)]
    [TestCase("DE89370400440532013001", false)]
    [TestCase("GB29NWBK60161331926819", true)]
    [TestCase("1234", false)]
    public void IsValidIban_ChecksMod97(string iban, bool valid) => GermanTaxId.IsValidIban(iban).ShouldBe(valid);
}

[TestFixture]
public sealed class GermanyComplianceModuleTests
{
    private readonly GermanyComplianceModule _module = new();

    [Test]
    public void ValidateIssuer_AComplete_IsAccepted() => _module.ValidateIssuer(XRechnungTestData.Issuer()).ShouldBeEmpty();

    [Test]
    public void ValidateIssuer_AUstIdNrAsTaxId_IsAccepted() =>
        _module.ValidateIssuer(XRechnungTestData.Issuer(taxId: "DE123456788")).ShouldBeEmpty();

    [Test]
    public void ValidateIssuer_BadIdsAndPostcode_SaySo()
    {
        var issuer = XRechnungTestData.Issuer(taxId: "12") with
        {
            Address = new Address("Hauptstraße", "5", "Berlin", "1011", "Germany"),
            Values = new Dictionary<string, string>(XRechnungTestData.Issuer().Values)
            {
                [GermanyComplianceModule.VatIdKey] = "DE123456789",
                [GermanyComplianceModule.IbanKey] = "DE00 0000"
            }
        };

        _module.ValidateIssuer(issuer).Select(i => i.Field).ShouldBe(
            ["taxId", GermanyComplianceModule.VatIdKey, "address", GermanyComplianceModule.IbanKey], ignoreOrder: true);
    }
}

[TestFixture]
public sealed class XRechnungFormatTests
{
    private static readonly XNamespace Cbc = XRechnungXmlBuilder.Cbc;
    private static readonly XNamespace Cac = XRechnungXmlBuilder.Cac;

    private readonly XRechnungFormat _format = new(new GermanyComplianceModule());

    private XElement Build(EInvoiceDocument document)
    {
        _format.Validate(document).ShouldBeEmpty();
        var artifact = _format.Build(document, null);
        artifact.FileExtension.ShouldBe("xml");
        return XDocument.Parse(System.Text.Encoding.UTF8.GetString(artifact.Content)).Root!;
    }

    private static string Value(XElement root, params XName[] path) =>
        path.Aggregate(root, (e, name) => e.Element(name) ?? throw new InvalidOperationException($"{name} missing in {e.Name}")).Value;

    [Test]
    public void Build_StandardRate_WritesTheXRechnungProfile_TheVat_AndHowToPay()
    {
        var root = Build(XRechnungTestData.Invoice(XRechnungTestData.Issuer(), XRechnungTestData.GermanCustomer(), [XRechnungTestData.Vat(19m, 1000m)]));

        root.Name.ShouldBe(XRechnungXmlBuilder.Ubl + "Invoice");
        Value(root, Cbc + "CustomizationID").ShouldBe(XRechnungXmlBuilder.CustomizationId);
        Value(root, Cbc + "InvoiceTypeCode").ShouldBe("380");
        Value(root, Cbc + "BuyerReference").ShouldBe("2026-042");

        var seller = root.Element(Cac + "AccountingSupplierParty")!.Element(Cac + "Party")!;
        seller.Element(Cbc + "EndpointID")!.Attribute("schemeID")!.Value.ShouldBe("EM");
        seller.Elements(Cac + "PartyTaxScheme").Select(s => (Value(s, Cbc + "CompanyID"), Value(s, Cac + "TaxScheme", Cbc + "ID")))
            .ShouldBe([("DE123456788", "VAT"), ("21/815/08150", "FC")]);
        Value(seller, Cac + "Contact", Cbc + "Telephone").ShouldBe("+49 30 1234567");

        Value(root, Cac + "PaymentMeans", Cbc + "PaymentMeansCode").ShouldBe("58");
        Value(root, Cac + "PaymentMeans", Cac + "PayeeFinancialAccount", Cbc + "ID").ShouldBe("DE89370400440532013000");
        Value(root, Cac + "PaymentTerms", Cbc + "Note").ShouldContain("14.11.2026");

        Value(root, Cac + "TaxTotal", Cbc + "TaxAmount").ShouldBe("190.00");
        Value(root, Cac + "TaxTotal", Cac + "TaxSubtotal", Cac + "TaxCategory", Cbc + "ID").ShouldBe("S");
        Value(root, Cac + "LegalMonetaryTotal", Cbc + "TaxInclusiveAmount").ShouldBe("1190.00");
        Value(root, Cac + "LegalMonetaryTotal", Cbc + "PayableAmount").ShouldBe("1190.00");
        Value(root, Cac + "InvoiceLine", Cac + "Item", Cac + "ClassifiedTaxCategory", Cbc + "Percent").ShouldBe("19.00");
    }

    [Test]
    public void Build_Kleinunternehmer_IsExemptWithTheParagraph19Note()
    {
        var root = Build(XRechnungTestData.Invoice(XRechnungTestData.Issuer(smallBusiness: true), XRechnungTestData.GermanCustomer(), []));

        root.Elements(Cbc + "Note").Select(n => n.Value).ShouldContain(GermanyComplianceModule.SmallBusinessNote);
        var category = root.Element(Cac + "TaxTotal")!.Element(Cac + "TaxSubtotal")!.Element(Cac + "TaxCategory")!;
        Value(category, Cbc + "ID").ShouldBe("E");
        Value(category, Cbc + "Percent").ShouldBe("0.00");
        Value(category, Cbc + "TaxExemptionReason").ShouldBe(GermanyComplianceModule.SmallBusinessNote);
        Value(root, Cac + "LegalMonetaryTotal", Cbc + "PayableAmount").ShouldBe("1000.00");
    }

    [Test]
    public void Build_ReverseChargeToAnEuBusiness_IsAE_WithItsCode()
    {
        var zero = XRechnungTestData.Vat(0m, 1000m, new Dictionary<string, string> { [GermanyComplianceModule.CategoryKey] = GermanyComplianceModule.ReverseCharge });

        var root = Build(XRechnungTestData.Invoice(XRechnungTestData.Issuer(), XRechnungTestData.FrenchCustomer(), [zero]));

        var category = root.Element(Cac + "TaxTotal")!.Element(Cac + "TaxSubtotal")!.Element(Cac + "TaxCategory")!;
        Value(category, Cbc + "ID").ShouldBe("AE");
        Value(category, Cbc + "TaxExemptionReasonCode").ShouldBe("VATEX-EU-AE");
        Value(root, Cac + "AccountingCustomerParty", Cac + "Party", Cac + "PartyTaxScheme", Cbc + "CompanyID").ShouldBe("FR40303265045");
        Value(root, Cac + "AccountingCustomerParty", Cac + "Party", Cac + "PostalAddress", Cac + "Country", Cbc + "IdentificationCode").ShouldBe("FR");
    }

    [Test]
    public void Build_NotSubject_HasNoVatIdentifiers_NoLineRate_AndTheSteuernummerAsSellerId()
    {
        var zero = XRechnungTestData.Vat(0m, 1000m, new Dictionary<string, string> { [GermanyComplianceModule.CategoryKey] = GermanyComplianceModule.NotSubject });

        var root = Build(XRechnungTestData.Invoice(XRechnungTestData.Issuer(), XRechnungTestData.FrenchCustomer(), [zero]));

        root.Descendants(Cac + "PartyTaxScheme").Select(s => Value(s, Cac + "TaxScheme", Cbc + "ID")).ShouldBe(["FC"]);
        Value(root, Cac + "AccountingSupplierParty", Cac + "Party", Cac + "PartyIdentification", Cbc + "ID").ShouldBe("21/815/08150");
        root.Element(Cac + "InvoiceLine")!.Descendants(Cbc + "Percent").ShouldBeEmpty();
        // XRechnung wants the breakdown's rate even then (BR-DE-14)
        Value(root, Cac + "TaxTotal", Cac + "TaxSubtotal", Cac + "TaxCategory", Cbc + "Percent").ShouldBe("0.00");
    }

    [Test]
    public void Validate_NotSubjectWithTheUstIdNrAsTaxId_AsksForTheSteuernummer()
    {
        var zero = XRechnungTestData.Vat(0m, 1000m, new Dictionary<string, string> { [GermanyComplianceModule.CategoryKey] = GermanyComplianceModule.NotSubject });

        _format.Validate(XRechnungTestData.Invoice(XRechnungTestData.Issuer(taxId: "DE123456788"), XRechnungTestData.FrenchCustomer(), [zero]))
            .ShouldContain(i => i.Field == "taxId" && i.Message.Contains("Steuernummer"));
    }

    [Test]
    public void Validate_MissingContactIbanAndBuyerEmail_AreReported()
    {
        var values = new Dictionary<string, string>(XRechnungTestData.Issuer().Values);
        values.Remove(GermanyComplianceModule.ContactPhoneKey);
        values.Remove(GermanyComplianceModule.IbanKey);
        var issuer = XRechnungTestData.Issuer() with { Values = values };

        var issues = _format.Validate(XRechnungTestData.Invoice(issuer, XRechnungTestData.GermanCustomer(email: ""), [XRechnungTestData.Vat(19m, 1000m)]));

        issues.Select(i => i.Message).ShouldContain(m => m.Contains("contact phone"));
        issues.Select(i => i.Message).ShouldContain(m => m.Contains("IBAN"));
        issues.Select(i => i.Message).ShouldContain(m => m.Contains("email address their e-invoices"));
    }

    [Test]
    public void Validate_KleinunternehmerChargingVat_IsRefused() =>
        _format.Validate(XRechnungTestData.Invoice(XRechnungTestData.Issuer(smallBusiness: true), XRechnungTestData.GermanCustomer(), [XRechnungTestData.Vat(19m, 1000m)]))
            .ShouldContain(i => i.Message.Contains("Kleinunternehmer charges no VAT"));

    [Test]
    public void Validate_ReverseChargeWithoutTheCustomersVatId_IsRefused()
    {
        var zero = XRechnungTestData.Vat(0m, 1000m, new Dictionary<string, string> { [GermanyComplianceModule.CategoryKey] = GermanyComplianceModule.ReverseCharge });
        var buyer = XRechnungTestData.FrenchCustomer() with { TaxId = "" };

        _format.Validate(XRechnungTestData.Invoice(XRechnungTestData.Issuer(), buyer, [zero]))
            .ShouldContain(i => i.Message.Contains("customer's VAT identifier"));
    }

    [Test]
    public void Validate_ZeroVatWithoutACategory_AsksForIt() =>
        _format.Validate(XRechnungTestData.Invoice(XRechnungTestData.Issuer(), XRechnungTestData.GermanCustomer(), [XRechnungTestData.Vat(0m, 1000m)]))
            .ShouldContain(i => i.Message.Contains("choose why no VAT"));

    [Test]
    public void Validate_AWithholding_IsRefused()
    {
        var withheld = new EInvoiceTax(EInvoiceTaxKind.Withheld, "Abzug", 10m, 1000m, 100m);
        var document = XRechnungTestData.Invoice(XRechnungTestData.Issuer(), XRechnungTestData.GermanCustomer(), [XRechnungTestData.Vat(19m, 1000m), withheld]);

        _format.Validate(document).ShouldContain(i => i.Message.Contains("no withholding"));
    }
}

[TestFixture]
public sealed class GermanyTemplateContributorTests
{
    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [Test]
    public async Task Contribute_Kleinunternehmer_GivesTheNoteAndTheSteuernummer()
    {
        var settings = new ComplianceSettings(GermanyComplianceModule.CountryCodeValue);
        settings.Update(true, "Anna Schmidt", "21/815/08150", null, new Dictionary<string, string> { [GermanyComplianceModule.SmallBusinessKey] = "true" });
        var gate = new Mock<IComplianceGate>();
        gate.Setup(g => g.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ActiveCompliance(new GermanyComplianceModule(), settings)]);
        var invoice = new Invoice(1, new InvoiceNumber("2026-042"), InvoiceType.OneTime, new DateOnly(2026, 10, 15), new Money(1000m, "EUR"), "EUR");

        var model = (await new GermanyTemplateContributor(gate.Object, new InMemoryRepository<Tax>()).ContributeAsync(invoice, Token))
            .ShouldBeOfType<GermanyTemplateModel>();

        model.IsApplicable.ShouldBeTrue();
        model.IsSmallBusiness.ShouldBeTrue();
        model.TaxNumber.ShouldBe("21/815/08150");
        model.HasVatNote.ShouldBeTrue();
        model.VatNote.ShouldBe(GermanyComplianceModule.SmallBusinessNote);
    }

    [Test]
    public async Task Contribute_WithoutTheGermanRules_GivesNothing()
    {
        var gate = new Mock<IComplianceGate>();
        gate.Setup(g => g.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var invoice = new Invoice(1, new InvoiceNumber("2026-042"), InvoiceType.OneTime, new DateOnly(2026, 10, 15), new Money(1000m, "EUR"), "EUR");

        (await new GermanyTemplateContributor(gate.Object, new InMemoryRepository<Tax>()).ContributeAsync(invoice, Token)).ShouldBeNull();
    }
}
