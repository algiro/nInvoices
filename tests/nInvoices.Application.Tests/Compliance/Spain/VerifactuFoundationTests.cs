using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using nInvoices.Application.Compliance.Spain.Verifactu;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance.Spain;

[TestFixture]
public sealed class VerifactuFoundationTests
{
    // --- The hash: AEAT test vectors (huella v0.1.2, section 6) ---------------------------------

    private const string Case1 = "3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60";
    private const string Case2 = "F7B94CFD8924EDFF273501B01EE5153E4CE8F259766F88CF6ACB8935802A2B97";
    private const string Case3 = "177547C0D57AC74748561D054A9CEC14B4C4EA23D1BEFD6F2E69E3A388F90C68";

    [Test]
    public void Hash_FirstRecord_MatchesAeatExample1() =>
        VerifactuHash.Issued("89890001K", "12345678/G33", "01-01-2024", "F1", "12.35", "123.45", "", "2024-01-01T19:20:30+01:00")
            .ShouldBe(Case1);

    [Test]
    public void Hash_SecondRecord_MatchesAeatExample2() =>
        VerifactuHash.Issued("89890001K", "12345679/G34", "01-01-2024", "F1", "12.35", "123.45", Case1, "2024-01-01T19:20:35+01:00")
            .ShouldBe(Case2);

    [Test]
    public void Hash_Cancellation_MatchesAeatExample3() =>
        VerifactuHash.Cancelled("89890001K", "12345679/G34", "01-01-2024", Case2, "2024-01-01T19:20:40+01:00")
            .ShouldBe(Case3);

    [Test]
    public void Hash_ValuesAreTrimmed_AsTheSpecificationAsks() =>
        VerifactuHash.Issued(" 89890001K ", "  12345678/G33  ", "01-01-2024", "F1", "12.35", "123.45", "", "2024-01-01T19:20:30+01:00")
            .ShouldBe(Case1);

    [Test]
    public void Hash_ChangesWithAnyField()
    {
        var baseline = VerifactuHash.Issued("89890001K", "1", "01-01-2024", "F1", "12.35", "123.45", "", "2024-01-01T19:20:30+01:00");

        VerifactuHash.Issued("89890001K", "1", "01-01-2024", "F1", "12.35", "123.46", "", "2024-01-01T19:20:30+01:00").ShouldNotBe(baseline);
        VerifactuHash.Issued("89890001K", "1", "01-01-2024", "F1", "12.35", "123.45", "ABC", "2024-01-01T19:20:30+01:00").ShouldNotBe(baseline);
    }

    [Test]
    public void Amount_UsesTwoDecimalsAndADot() =>
        new[] { VerifactuHash.Amount(123.1m), VerifactuHash.Amount(1701.1155m), VerifactuHash.Amount(0.005m) }
            .ShouldBe(["123.10", "1701.12", "0.01"]);

    [Test]
    public void Date_IsDayMonthYear() =>
        VerifactuHash.Date(new DateOnly(2024, 1, 1)).ShouldBe("01-01-2024");

    // --- The time stamp --------------------------------------------------------------------------------

    [TestCase("2026-10-03T09:30:00Z", "2026-10-03T11:30:00+02:00")] // summer time
    [TestCase("2026-01-15T10:00:00Z", "2026-01-15T11:00:00+01:00")] // winter time
    public void Stamp_UsesTheOffsetInForceInSpain(string utc, string expected) =>
        SpanishClock.Stamp(DateTimeOffset.Parse(utc)).ShouldBe(expected);

    // --- The QR code: AEAT examples (QR v0.5.0, sections 4 and 8) ----------------------------------

    [Test]
    public void QrUrl_EncodesTheParameters_LikeTheAeatExample() =>
        VerifactuQr.Url(production: false, "89890001K", "12345678&G33", "01-01-2024", "241.4")
            .ShouldBe("https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR?nif=89890001K&numserie=12345678%26G33&fecha=01-01-2024&importe=241.4");

    [Test]
    public void QrUrl_Production_UsesTheProductionService() =>
        VerifactuQr.Url(production: true, "89890001K", "12345678-G33", "01-09-2024", "241.4")
            .ShouldBe("https://www2.agenciatributaria.gob.es/wlpl/TIKE-CONT/ValidarQR?nif=89890001K&numserie=12345678-G33&fecha=01-09-2024&importe=241.4");

    [Test]
    public void QrUrl_SpacesAndSlashesAreEncoded() =>
        VerifactuQr.Url(false, "89890001K", "A 12/3", "01-01-2024", "1.00")
            .ShouldContain("numserie=A%2012%2F3&");

    [Test]
    public void QrSvg_IsAnImage()
    {
        var url = VerifactuQr.Url(false, "89890001K", "12345678-G33", "01-09-2024", "241.40");

        VerifactuQr.Svg(url).ShouldStartWith("<svg");
        VerifactuQr.SvgDataUri(url).ShouldStartWith("data:image/svg+xml;base64,");
    }

    // --- The XML: validated against AEAT's own schemas -------------------------------------------

    private static readonly VerifactuSystem System = new("Producer SL", "B12345674", "nInvoices", "NI", "1.0.0", "1", false);

    private static VerifactuInvoiceData Invoice(
        VerifactuRecipient? recipient = null, IReadOnlyList<VerifactuBreakdown>? breakdown = null) => new(
        "12345678Z", "Ana Pérez García", "26-10-001", new DateOnly(2026, 10, 3), "F1", "Servicios profesionales",
        recipient ?? new VerifactuRecipient("Cliente SA", "A58818501", null, null, null),
        breakdown ?? [new VerifactuBreakdown("S1", 21m, 8100.55m, 1701.12m)],
        1701.12m, 9801.67m);

    private static readonly Lazy<XmlSchemaSet> Schemas = new(() =>
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Compliance", "Spain", "Schemas");
        var set = new XmlSchemaSet { XmlResolver = null };
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
        foreach (var file in new[] { "xmldsig-core-schema.xsd", "SuministroInformacion.xsd", "SuministroLR.xsd" })
        {
            using var reader = XmlReader.Create(Path.Combine(directory, file), settings);
            set.Add(null, reader);
        }

        set.Compile();
        return set;
    });

    private static List<string> SchemaErrors(XElement element)
    {
        var errors = new List<string>();
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = Schemas.Value, XmlResolver = null };
        settings.ValidationEventHandler += (_, e) => errors.Add($"{e.Severity}: {e.Message}");

        using var reader = XmlReader.Create(new StringReader(element.ToString(SaveOptions.DisableFormatting)), settings);
        while (reader.Read()) { }
        return errors;
    }

    private static string Stamp => "2026-10-03T11:30:00+02:00";

    private static VerifactuPrevious Previous => new("12345678Z", "26-09-004", "30-09-2026", Case1);

    [Test]
    public void Xml_FirstRecord_IsValidAgainstTheAeatSchema()
    {
        var record = VerifactuXml.Issued(Invoice(), System, previous: null, Stamp, Case1);

        SchemaErrors(VerifactuXml.Batch("Ana Pérez García", "12345678Z", [record])).ShouldBeEmpty();
        record.Element(VerifactuXml.Sf + "Encadenamiento")!.Element(VerifactuXml.Sf + "PrimerRegistro")!.Value.ShouldBe("S");
    }

    [Test]
    public void Xml_ChainedRecord_CarriesThePreviousRecord()
    {
        var record = VerifactuXml.Issued(Invoice(), System, Previous, Stamp, Case2);

        SchemaErrors(VerifactuXml.Batch("Ana Pérez García", "12345678Z", [record])).ShouldBeEmpty();
        var previous = record.Element(VerifactuXml.Sf + "Encadenamiento")!.Element(VerifactuXml.Sf + "RegistroAnterior")!;
        previous.Element(VerifactuXml.Sf + "Huella")!.Value.ShouldBe(Case1);
        previous.Element(VerifactuXml.Sf + "FechaExpedicionFactura")!.Value.ShouldBe("30-09-2026");
    }

    [Test]
    public void Xml_CustomerAbroad_IsIdentifiedByAForeignId()
    {
        var recipient = new VerifactuRecipient("Cliente Srl", null, "IT", "02", "IT01234567890");
        var breakdown = new[] { new VerifactuBreakdown("N2", 0m, 8100.55m, 0m) };

        var record = VerifactuXml.Issued(Invoice(recipient, breakdown), System, null, Stamp, Case1);

        SchemaErrors(VerifactuXml.Batch("Ana", "12345678Z", [record])).ShouldBeEmpty();
        record.Descendants(VerifactuXml.Sf + "IDOtro").Single().Element(VerifactuXml.Sf + "CodigoPais")!.Value.ShouldBe("IT");
    }

    [TestCase("S1")]
    [TestCase("S2")]
    [TestCase("N1")]
    [TestCase("N2")]
    [TestCase("E1")]
    [TestCase("E5")]
    [TestCase("E6")]
    public void Xml_EveryKindOfOperation_IsValid(string operation)
    {
        var breakdown = new[] { new VerifactuBreakdown(operation, operation == "S1" ? 21m : 0m, 100m, operation == "S1" ? 21m : 0m) };

        var record = VerifactuXml.Issued(Invoice(breakdown: breakdown), System, null, Stamp, Case1);

        SchemaErrors(VerifactuXml.Batch("Ana", "12345678Z", [record])).ShouldBeEmpty();
    }

    [Test]
    public void Xml_CancellationRecord_IsValid()
    {
        var record = VerifactuXml.Cancelled("12345678Z", "26-10-001", new DateOnly(2026, 10, 3), System, Previous, Stamp, Case3);

        SchemaErrors(VerifactuXml.Batch("Ana", "12345678Z", [record])).ShouldBeEmpty();
    }

    [Test]
    public void Xml_SeveralRecordsInOneBatch_AreValid()
    {
        var first = VerifactuXml.Issued(Invoice(), System, null, Stamp, Case1);
        var cancel = VerifactuXml.Cancelled("12345678Z", "26-10-001", new DateOnly(2026, 10, 3), System, Previous, Stamp, Case3);

        SchemaErrors(VerifactuXml.Batch("Ana", "12345678Z", [first, cancel])).ShouldBeEmpty();
    }

    [Test]
    public void Xml_ABrokenRecord_IsCaughtByTheSchema()
    {
        // The check is real: a wrong operation code is rejected
        var breakdown = new[] { new VerifactuBreakdown("ZZ", 0m, 100m, 0m) };
        var record = VerifactuXml.Issued(Invoice(breakdown: breakdown), System, null, Stamp, Case1);

        SchemaErrors(VerifactuXml.Batch("Ana", "12345678Z", [record])).ShouldNotBeEmpty();
    }
}
