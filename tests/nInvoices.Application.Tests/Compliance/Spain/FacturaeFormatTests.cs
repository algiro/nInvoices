using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using nInvoices.Application.Compliance.Spain;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance.Spain;

[TestFixture]
public sealed class FacturaeFormatTests
{
    private X509Certificate2 _certificate = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => _certificate = FacturaeTestData.NewCertificate();

    [OneTimeTearDown]
    public void OneTimeTearDown() => _certificate.Dispose();

    private static string Xml(EInvoiceArtifact artifact) => Encoding.UTF8.GetString(artifact.Content);

    private EInvoiceArtifact Build(EInvoiceDocument document) => FacturaeTestData.Format().Build(document, _certificate);

    private static XmlDocument Parse(EInvoiceArtifact artifact)
    {
        var document = new XmlDocument { PreserveWhitespace = true };
        document.LoadXml(Xml(artifact));
        return document;
    }

    private static bool SignatureIsValid(XmlDocument document)
    {
        var signature = (XmlElement)document.GetElementsByTagName("Signature", "http://www.w3.org/2000/09/xmldsig#")[0]!;
        var signed = new SignedXml(document);
        signed.LoadXml(signature);
        // Verified with the public key carried in the signature's own KeyInfo
        return signed.CheckSignature();
    }

    // --- Schema and signature ----------------------------------------------------------------

    [Test]
    public void Build_IndividualIssuerToPublicAdministration_ProducesSchemaValidSignedFacturae()
    {
        var artifact = Build(FacturaeTestData.Invoice());

        FacturaeTestData.SchemaErrors(artifact.Content).ShouldBeEmpty();
        artifact.ContentType.ShouldBe("application/xml");
        artifact.FileExtension.ShouldBe("xsig");
        Xml(artifact).ShouldContain("<RoleTypeCode>01</RoleTypeCode>");
        Xml(artifact).ShouldContain("<CentreCode>GE0001234</CentreCode>");
    }

    [Test]
    public void Build_LegalEntityIssuerToCompany_IsSchemaValid()
    {
        var artifact = Build(FacturaeTestData.Invoice(FacturaeTestData.LegalEntity(), FacturaeTestData.Company(), withholding: false));

        FacturaeTestData.SchemaErrors(artifact.Content).ShouldBeEmpty();
        Xml(artifact).ShouldContain("<CorporateName>Consultoría Pérez SL</CorporateName>");
        Xml(artifact).ShouldNotContain("<TaxesWithheld>");
    }

    [Test]
    public void Build_BuyerInAnotherEuCountry_UsesAnOverseasAddress()
    {
        var artifact = Build(FacturaeTestData.Invoice(buyer: FacturaeTestData.ItalianCompany()));

        FacturaeTestData.SchemaErrors(artifact.Content).ShouldBeEmpty();
        Xml(artifact).ShouldContain("<OverseasAddress>");
        Xml(artifact).ShouldContain("<ResidenceTypeCode>U</ResidenceTypeCode>");
        Xml(artifact).ShouldContain("<CountryCode>ITA</CountryCode>");
        Xml(artifact).ShouldContain("<TaxIdentificationNumber>IT01234567890</TaxIdentificationNumber>");
    }

    [Test]
    public void Build_IndividualBuyer_UsesNameAndSurnames()
    {
        var buyer = new EInvoiceParty(
            "Luis",
            "87654321X",
            new Address("Calle Sol", "3", "Sevilla", "41001", "Spain", "Sevilla"),
            new Dictionary<string, string> { [SpainComplianceModule.FirstSurnameKey] = "Gómez" });

        var artifact = Build(FacturaeTestData.Invoice(buyer: buyer));

        FacturaeTestData.SchemaErrors(artifact.Content).ShouldBeEmpty();
        Xml(artifact).ShouldContain("<FirstSurname>Gómez</FirstSurname>");
    }

    [Test]
    public void Build_Signature_VerifiesAgainstItsOwnKeyInfo()
    {
        var document = Parse(Build(FacturaeTestData.Invoice()));

        SignatureIsValid(document).ShouldBeTrue();
    }

    [Test]
    public void Build_TamperedInvoice_FailsSignatureVerification()
    {
        var document = Parse(Build(FacturaeTestData.Invoice()));
        document.GetElementsByTagName("InvoiceTotal")[0]!.InnerText = "1.00";

        SignatureIsValid(document).ShouldBeFalse();
    }

    [Test]
    public void Build_Signature_CarriesTheFacturaePolicyAndCertificate()
    {
        var xml = Xml(Build(FacturaeTestData.Invoice()));

        xml.ShouldContain("politica_de_firma_formato_facturae_v3_1.pdf");
        xml.ShouldContain("<xades:SigningTime>2026-10-03T09:30:00Z</xades:SigningTime>");
        xml.ShouldContain(Convert.ToBase64String(_certificate.RawData));
    }

    [Test]
    public void Build_SignedDocument_IsStillValidAfterAReparse()
    {
        // What FACe receives is the bytes, so check those bytes, not the in-memory document
        var artifact = Build(FacturaeTestData.Invoice());
        var reparsed = new XmlDocument { PreserveWhitespace = true };
        reparsed.Load(new MemoryStream(artifact.Content));

        SignatureIsValid(reparsed).ShouldBeTrue();
    }

    // --- Amounts ----------------------------------------------------------------------------------

    [Test]
    public void Build_Totals_AreGrossPlusTaxesMinusWithholdings()
    {
        var xml = Xml(Build(FacturaeTestData.Invoice()));

        xml.ShouldContain("<TotalGrossAmount>8100.55</TotalGrossAmount>");
        xml.ShouldContain("<TotalTaxOutputs>1701.12</TotalTaxOutputs>");
        xml.ShouldContain("<TotalTaxesWithheld>1215.08</TotalTaxesWithheld>");
        xml.ShouldContain("<InvoiceTotal>9801.67</InvoiceTotal>");
        xml.ShouldContain("<TotalOutstandingAmount>8586.59</TotalOutstandingAmount>");
    }

    [Test]
    public void Build_LineTaxes_AddUpToTheInvoiceTaxEvenWithRounding()
    {
        // Three lines of 33.33 at 21%: 7.00 each, 21.00 of the invoice-level 21.00; 0.33 cents of rounding is absorbed
        var document = FacturaeTestData.Invoice(withholding: false) with
        {
            Lines =
            [
                new EInvoiceLine("A", 1m, 33.33m, 33.33m),
                new EInvoiceLine("B", 1m, 33.33m, 33.33m),
                new EInvoiceLine("C", 1m, 33.33m, 33.33m)
            ],
            Taxes = [new EInvoiceTax(EInvoiceTaxKind.Added, "VAT", 10m, 99.99m, 10.00m)],
            Total = 109.99m
        };

        var artifact = Build(document);

        FacturaeTestData.SchemaErrors(artifact.Content).ShouldBeEmpty();
        var parsed = Parse(artifact);
        var lineAmounts = parsed.SelectNodes("//InvoiceLine/TaxesOutputs/Tax/TaxAmount/TotalAmount")!
            .Cast<XmlNode>().Select(n => decimal.Parse(n.InnerText, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        lineAmounts.Sum().ShouldBe(10.00m);
    }

    [Test]
    public void Build_UnitPrice_IsDerivedWhenQuantityTimesPriceDoesNotMatchTheAmount()
    {
        var document = FacturaeTestData.Invoice(withholding: false) with
        {
            Lines = [new EInvoiceLine("Project hours", 3m, 33.33m, 100m)],
            Taxes = [new EInvoiceTax(EInvoiceTaxKind.Added, "VAT", 21m, 100m, 21m)],
            Total = 121m
        };

        var xml = Xml(Build(document));

        xml.ShouldContain("<UnitPriceWithoutTax>33.33333333</UnitPriceWithoutTax>");
        xml.ShouldContain("<TotalCost>100.00</TotalCost>");
    }

    // --- Validation ------------------------------------------------------------------------------

    private static List<string> Problems(EInvoiceDocument document) =>
        FacturaeTestData.Format().Validate(document).Select(i => i.Message).ToList();

    [Test]
    public void Validate_CompleteInvoice_HasNoIssues() =>
        Problems(FacturaeTestData.Invoice()).ShouldBeEmpty();

    [Test]
    public void Validate_PublicAdministrationWithoutCodes_IsReported()
    {
        var buyer = FacturaeTestData.PublicAdministration() with
        {
            Values = new Dictionary<string, string> { [SpainComplianceModule.IsPublicAdministrationKey] = "true" }
        };

        Problems(FacturaeTestData.Invoice(buyer: buyer))
            .Count(p => p.Contains("DIR3")).ShouldBe(3);
    }

    [Test]
    public void Validate_SpanishCustomerWithABadNif_IsReported()
    {
        var buyer = FacturaeTestData.Company() with { TaxId = "A58818502" };

        Problems(FacturaeTestData.Invoice(buyer: buyer)).ShouldContain(p => p.Contains("not a valid Spanish NIF"));
    }

    [Test]
    public void Validate_NumberLongerThanTwentyCharacters_IsReported()
    {
        var document = FacturaeTestData.Invoice() with { Number = "2026-10-INVOICE-0000001" };

        Problems(document).ShouldContain(p => p.Contains("at most 20 characters"));
    }

    [Test]
    public void Validate_NoTax_IsReported()
    {
        var document = FacturaeTestData.Invoice() with { Taxes = [], Total = 8100.55m };

        Problems(document).ShouldContain(p => p.Contains("at least one tax"));
    }

    [Test]
    public void Validate_TaxCalculatedOnAnotherBase_IsReported()
    {
        // A tax on a tax: its base is not the invoice total
        var taxes = FacturaeTestData.Invoice().Taxes.Append(new EInvoiceTax(EInvoiceTaxKind.Added, "Surcharge", 5.2m, 1701.12m, 88.46m)).ToList();

        Problems(FacturaeTestData.Invoice() with { Taxes = taxes })
            .ShouldContain(p => p.Contains("Surcharge") && p.Contains("Facturae cannot express"));
    }

    [Test]
    public void Validate_TotalThatDoesNotMatch_IsReported()
    {
        Problems(FacturaeTestData.Invoice() with { Total = 1m })
            .ShouldContain(p => p.Contains("do not add up"));
    }

    [Test]
    public void Validate_IssuerOutsideSpain_IsReported()
    {
        var issuer = FacturaeTestData.Individual() with
        {
            Address = new Address("Via Roma", "5", "Milano", "20121", "Italy", "MI")
        };

        Problems(FacturaeTestData.Invoice(issuer)).ShouldContain(p => p.Contains("address must be in Spain"));
    }

    [Test]
    public void Validate_PublicAdministrationOutsideSpain_IsReported()
    {
        var buyer = FacturaeTestData.PublicAdministration() with
        {
            Address = new Address("Via Roma", "5", "Milano", "20121", "Italy", "MI")
        };

        Problems(FacturaeTestData.Invoice(buyer: buyer)).ShouldContain(p => p.Contains("must have an address in Spain"));
    }

    [Test]
    public void Build_InvalidInvoice_Throws()
    {
        Should.Throw<InvalidOperationException>(() => Build(FacturaeTestData.Invoice() with { Total = 1m }))
            .Message.ShouldContain("not valid Facturae");
    }

    [Test]
    public void Build_WithoutCertificate_Throws()
    {
        Should.Throw<InvalidOperationException>(() => FacturaeTestData.Format().Build(FacturaeTestData.Invoice(), null))
            .Message.ShouldContain("signing certificate");
    }

    // --- IGIC (Canary Islands) -------------------------------------------------------------------

    private static EInvoiceDocument IgicInvoice() => FacturaeTestData.Invoice(withholding: false) with
    {
        Taxes =
        [
            new EInvoiceTax(EInvoiceTaxKind.Added, "IGIC 7%", 7m, 8100.55m, 567.04m,
                new Dictionary<string, string> { [SpainComplianceModule.TaxTypeKey] = SpainComplianceModule.Igic }),
            new EInvoiceTax(EInvoiceTaxKind.Withheld, "IRPF -15%", 15m, 8100.55m, 1215.08m)
        ],
        Total = 7452.51m
    };

    [Test]
    public void Build_Igic_IsTaxType03_AndTheWithholdingStaysIrpf()
    {
        var artifact = Build(IgicInvoice());

        FacturaeTestData.SchemaErrors(artifact.Content).ShouldBeEmpty();
        var parsed = Parse(artifact);
        parsed.SelectNodes("//TaxesOutputs/Tax/TaxTypeCode")!.Cast<XmlNode>().Select(n => n.InnerText).Distinct().ShouldBe(["03"]);
        parsed.SelectNodes("//TaxesWithheld/Tax/TaxTypeCode")!.Cast<XmlNode>().Select(n => n.InnerText).Distinct().ShouldBe(["04"]);
        parsed.SelectSingleNode("/*/Invoices/Invoice/TaxesOutputs/Tax/TaxRate")!.InnerText.ShouldBe("7.00");
    }

    [Test]
    public void Build_Igic_TotalsAreRightAndTheLinesAddUp()
    {
        var xml = Xml(Build(IgicInvoice()));

        xml.ShouldContain("<TotalTaxOutputs>567.04</TotalTaxOutputs>");
        xml.ShouldContain("<TotalTaxesWithheld>1215.08</TotalTaxesWithheld>");
        xml.ShouldContain("<InvoiceTotal>8667.59</InvoiceTotal>");
        xml.ShouldContain("<TotalOutstandingAmount>7452.51</TotalOutstandingAmount>");
    }

    [Test]
    public void Build_TaxWithoutATaxType_IsStillIva()
    {
        var parsed = Parse(Build(FacturaeTestData.Invoice()));

        parsed.SelectNodes("//TaxesOutputs/Tax/TaxTypeCode")!.Cast<XmlNode>().Select(n => n.InnerText).Distinct().ShouldBe(["01"]);
    }
}
