using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Schema;
using nInvoices.Application.Compliance.Spain;
using nInvoices.Application.Compliance.Spain.Facturae;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Tests.Compliance.Spain;

internal static class FacturaeTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 30, 0, TimeSpan.Zero);

    public static FacturaeFormat Format() => new(new SpainComplianceModule(), new FixedTimeProvider(Now.UtcDateTime));

    public static X509Certificate2 NewCertificate(string subject = "CN=Ana Perez Garcia, O=nInvoices tests, C=ES")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(Now.AddDays(-1), Now.AddYears(1));
        // Round-trips through PFX, as the app does with the uploaded certificate
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx, "pw"), "pw", X509KeyStorageFlags.Exportable);
    }

    public static IssuerProfile Individual() => new(
        "Ana Pérez García",
        "12345678Z",
        new Address("Calle Mayor", "1", "Madrid", "28013", "Spain", "Madrid"),
        new Dictionary<string, string>
        {
            [SpainComplianceModule.PersonTypeKey] = SpainComplianceModule.Individual,
            [SpainComplianceModule.FirstNameKey] = "Ana",
            [SpainComplianceModule.FirstSurnameKey] = "Pérez",
            [SpainComplianceModule.SecondSurnameKey] = "García"
        });

    public static IssuerProfile LegalEntity() => new(
        "Consultoría Pérez SL",
        "B12345674",
        new Address("Gran Vía", "10", "Madrid", "28013", "Spain", "Madrid"),
        new Dictionary<string, string> { [SpainComplianceModule.PersonTypeKey] = SpainComplianceModule.LegalEntity });

    /// <summary>A public administration (FACe): three DIR3 codes.</summary>
    public static EInvoiceParty PublicAdministration() => new(
        "Ayuntamiento de Prueba",
        "Q2826000H",
        new Address("Plaza Mayor", "1", "Madrid", "28001", "Spain", "Madrid"),
        new Dictionary<string, string>
        {
            [SpainComplianceModule.IsPublicAdministrationKey] = "true",
            [SpainComplianceModule.AccountingOfficeKey] = "L01280796",
            [SpainComplianceModule.ManagingBodyKey] = "L01280796",
            [SpainComplianceModule.ProcessingUnitKey] = "GE0001234"
        });

    public static EInvoiceParty Company() => new(
        "Cliente SA",
        "A58818501",
        new Address("Avinguda Diagonal", "100", "Barcelona", "08019", "Spain", "Barcelona"),
        new Dictionary<string, string>());

    public static EInvoiceParty ItalianCompany() => new(
        "Cliente Srl",
        "IT01234567890",
        new Address("Via Roma", "5", "Milano", "20121", "Italy", "MI"),
        new Dictionary<string, string>());

    /// <summary>8000.00 of services plus a 100.55 expense; 21% VAT and 15% income-tax withholding.</summary>
    public static EInvoiceDocument Invoice(
        IssuerProfile? issuer = null,
        EInvoiceParty? buyer = null,
        bool withholding = true)
    {
        var taxes = new List<EInvoiceTax>
        {
            new(EInvoiceTaxKind.Added, "VAT", 21m, 8100.55m, 1701.12m)
        };
        if (withholding)
            taxes.Add(new EInvoiceTax(EInvoiceTaxKind.Withheld, "IRPF", 15m, 8100.55m, 1215.08m));

        return new EInvoiceDocument(
            "26-10-001",
            new DateOnly(2026, 10, 3),
            new DateOnly(2026, 11, 2),
            "EUR",
            issuer ?? Individual(),
            buyer ?? PublicAdministration(),
            [
                new EInvoiceLine("Professional Services for September 2026", 20m, 400m, 8000m),
                new EInvoiceLine("Expense: train & taxi", 1m, 100.55m, 100.55m)
            ],
            taxes,
            withholding ? 8586.59m : 9801.67m,
            Notes: null,
            Language: "es");
    }

    private static readonly Lazy<XmlSchemaSet> Schemas = new(LoadSchemas);

    private static XmlSchemaSet LoadSchemas()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Compliance", "Spain", "Schemas");
        var set = new XmlSchemaSet { XmlResolver = null };

        // The XML Signature schema has a DOCTYPE; its external DTD is not needed
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
        foreach (var file in new[] { "xmldsig-core-schema.xsd", "Facturaev3_2_2.xsd" })
        {
            using var reader = XmlReader.Create(Path.Combine(directory, file), settings);
            set.Add(null, reader);
        }

        set.Compile();
        return set;
    }

    /// <returns>Every schema violation of the Facturae file; empty when it is valid.</returns>
    public static List<string> SchemaErrors(byte[] facturae)
    {
        var errors = new List<string>();
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = Schemas.Value, XmlResolver = null };
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        // The XAdES properties inside ds:Object are lax content (their schema is not loaded): only that is let through
        settings.ValidationEventHandler += (_, e) =>
        {
            if (!e.Message.Contains("Could not find schema information"))
                errors.Add($"{e.Severity}: {e.Message} (line {e.Exception?.LineNumber})");
        };

        using var reader = XmlReader.Create(new MemoryStream(facturae), settings);
        while (reader.Read()) { }
        return errors;
    }
}
