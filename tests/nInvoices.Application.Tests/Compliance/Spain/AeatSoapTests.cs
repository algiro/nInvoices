using System.Xml.Linq;
using nInvoices.Application.Compliance.Spain.Verifactu;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance.Spain;

[TestFixture]
public sealed class AeatSoapTests
{
    // --- Where the submission goes ----------------------------------------------------------------

    [TestCase(false, false, "https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP")]
    [TestCase(false, true, "https://prewww10.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP")]
    [TestCase(true, false, "https://www1.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP")]
    [TestCase(true, true, "https://www10.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP")]
    public void Target_PicksTheServiceOfTheWsdl(bool production, bool seal, string url) =>
        new AeatTarget(production, seal).Url.ShouldBe(url);

    // --- The request -----------------------------------------------------------------------------------

    [Test]
    public void Envelope_PutsTheBatchInTheBodyOfASoap11Message()
    {
        var batch = VerifactuXml.Batch("Ana", "12345678Z", []);

        var envelope = AeatSoap.Envelope(batch).Root!;

        envelope.Name.NamespaceName.ShouldBe("http://schemas.xmlsoap.org/soap/envelope/");
        envelope.Elements().Select(e => e.Name.LocalName).ShouldBe(["Header", "Body"]);
        envelope.Elements().Last().Elements().Single().Name.ShouldBe(VerifactuXml.Lr + "RegFactuSistemaFacturacion");
    }

    // --- The answer ------------------------------------------------------------------------------------

    [Test]
    public void TheTestAnswers_AreValidAgainstTheAeatSchema()
    {
        // The answers used throughout the tests have the real shape, so the parser is tested against it
        AeatTestData.SchemaErrors(AeatTestData.Answer("Correcto", 60, "A-1234", ("26-10-001", "Alta", "Correcto", null, null))).ShouldBeEmpty();
        AeatTestData.SchemaErrors(AeatTestData.Answer("Incorrecto", 60, null, ("26-10-001", "Alta", "Incorrecto", "1100", "Valor o formato incorrecto"))).ShouldBeEmpty();
    }

    [Test]
    public void Parse_AcceptedSubmission()
    {
        var answer = AeatSoap.ParseResponse(AeatTestData.Answer("Correcto", 60, "A-1234567890ABCDEF",
            ("26-10-001", "Alta", "Correcto", null, null),
            ("26-10-002", "Anulacion", "Correcto", null, null)));

        answer.Csv.ShouldBe("A-1234567890ABCDEF");
        answer.WaitSeconds.ShouldBe(60);
        answer.EnvelopeStatus.ShouldBe("Correcto");
        answer.Lines.Count.ShouldBe(2);
        answer.Lines[0].ShouldBe(new AeatLineResult("26-10-001", "Alta", "Correcto", null, null));
        answer.Lines[1].Operation.ShouldBe("Anulacion");
    }

    [Test]
    public void Parse_RecordWithErrors_CarriesTheCodeAndTheDescription()
    {
        var answer = AeatSoap.ParseResponse(AeatTestData.Answer("ParcialmenteCorrecto", 120, "A-1",
            ("26-10-001", "Alta", "Correcto", null, null),
            ("26-10-002", "Alta", "AceptadoConErrores", "2000", "El campo Huella no coincide"),
            ("26-10-003", "Alta", "Incorrecto", "1100", "Valor o formato incorrecto")));

        answer.EnvelopeStatus.ShouldBe("ParcialmenteCorrecto");
        answer.WaitSeconds.ShouldBe(120);
        answer.Lines.Select(l => l.Status).ShouldBe(["Correcto", "AceptadoConErrores", "Incorrecto"]);
        answer.Lines[1].ErrorCode.ShouldBe("2000");
        answer.Lines[2].ErrorDescription.ShouldBe("Valor o formato incorrecto");
    }

    [Test]
    public void Parse_WithoutAWaitTime_AssumesAMinute() =>
        AeatSoap.ParseResponse(AeatTestData.Answer("Correcto", 0, null, ("1", "Alta", "Correcto", null, null)).Replace("<tikR:TiempoEsperaEnvio>0</tikR:TiempoEsperaEnvio>", ""))
            .WaitSeconds.ShouldBe(60);

    [Test]
    public void Parse_SoapFault_IsAnError()
    {
        var ex = Should.Throw<AeatException>(() => AeatSoap.ParseResponse(AeatTestData.Fault));

        ex.Message.ShouldContain("4102");
    }

    [Test]
    public void Parse_NotXml_IsAnError() =>
        Should.Throw<AeatException>(() => AeatSoap.ParseResponse("<html>Service unavailable"));

    [Test]
    public void Parse_AnAnswerToSomethingElse_IsAnError() =>
        Should.Throw<AeatException>(() => AeatSoap.ParseResponse(
            @"<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Body><x/></env:Body></env:Envelope>"));
}
