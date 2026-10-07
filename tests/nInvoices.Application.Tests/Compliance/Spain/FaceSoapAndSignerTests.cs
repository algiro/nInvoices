using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Linq;
using nInvoices.Application.Compliance.Spain.Face;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance.Spain;

[TestFixture]
public sealed class FaceSoapAndSignerTests
{
    private const string WsuNs = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
    private const string WsseNs = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private const string DsNs = "http://www.w3.org/2000/09/xmldsig#";

    private static readonly FaceTarget Staging = new(false);
    private static readonly DateTime Now = new(2026, 10, 3, 9, 30, 0, DateTimeKind.Utc);

    private X509Certificate2 _certificate = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => _certificate = FacturaeTestData.NewCertificate();

    [OneTimeTearDown]
    public void OneTimeTearDown() => _certificate.Dispose();

    // --- Where requests go ----------------------------------------------------------------------

    [Test]
    public void Target_PicksTheStagingOrProductionService()
    {
        Staging.Url.ShouldBe("https://se-ws-face.redsara.es/proveedores/v1/factura");
        new FaceTarget(true).Url.ShouldBe("https://ws.face.gob.es/proveedores/v1/factura");
        FaceSoap.SoapAction(Staging, "enviarFactura").ShouldBe("\"https://se-ws-face.redsara.es/proveedores/v1/factura#enviarFactura\"");
    }

    // --- The request body (RPC style, literal; shaped by the WSDL) -------------------------------

    [Test]
    public void SendInvoice_PutsTheEmailAndTheBase64FileInTheRequestPart()
    {
        var xsig = new byte[] { 1, 2, 3, 250 };

        var body = FaceSoap.SendInvoice(Staging, "ana@example.com", "26-10-001.xsig", xsig);

        body.Name.ShouldBe(XNamespace.Get(Staging.Namespace) + "enviarFactura");
        var request = body.Elements().Single();
        request.Name.ShouldBe((XName)"request"); // the message part: unqualified
        request.Element("correo")!.Value.ShouldBe("ana@example.com");
        var file = request.Element("factura")!;
        file.Element("factura")!.Value.ShouldBe(Convert.ToBase64String(xsig));
        file.Element("nombre")!.Value.ShouldBe("26-10-001.xsig");
        body.ToString().ShouldNotContain("xmlns=\"\""); // no stray default-namespace resets
    }

    [Test]
    public void GetInvoice_AsksForTheRegistryCode()
    {
        var body = FaceSoap.GetInvoice(Staging, "REG-123");

        body.Name.LocalName.ShouldBe("detalleFactura");
        body.Element("request")!.Element("codigoRegistro")!.Value.ShouldBe("REG-123");
    }

    // --- The signature ---------------------------------------------------------------------------

    private string Signed(bool sha1 = false) =>
        WsSecuritySigner.SignedEnvelope(FaceSoap.SendInvoice(Staging, "ana@example.com", "a.xsig", [1, 2, 3]), _certificate, sha1, Now);

    private static XmlDocument Load(string xml)
    {
        var document = new XmlDocument { PreserveWhitespace = true };
        document.LoadXml(xml);
        return document;
    }

    /// <summary>Exclusive canonical form of an element in its document (the way a receiving server sees it).</summary>
    private static byte[] Canonical(XmlDocument document, XmlElement element)
    {
        var transform = new XmlDsigExcC14NTransform();
        // The element, its attributes and its descendants, as a node set of the document
        var kept = new List<XmlNode>();
        void Collect(XmlNode node)
        {
            kept.Add(node);
            if (node.Attributes is not null) foreach (XmlAttribute a in node.Attributes) kept.Add(a);
            foreach (XmlNode child in node.ChildNodes) Collect(child);
        }
        Collect(element);
        var set = new XmlNodeListAdapter(kept);
        transform.LoadInput(set);
        using var output = (Stream)transform.GetOutput(typeof(Stream));
        using var memory = new MemoryStream();
        output.CopyTo(memory);
        return memory.ToArray();
    }

    private sealed class XmlNodeListAdapter(List<XmlNode> nodes) : XmlNodeList
    {
        public override int Count => nodes.Count;
        public override System.Collections.IEnumerator GetEnumerator() => nodes.GetEnumerator();
        public override XmlNode? Item(int index) => nodes[index];
    }

    /// <summary>Checks both the digest of the body and the signature the way a receiving server does.</summary>
    private static bool Verifies(XmlDocument document)
    {
        var signedInfo = (XmlElement)document.GetElementsByTagName("SignedInfo", DsNs)[0]!;
        var sha1 = signedInfo.InnerXml.Contains("#rsa-sha1");
        var body = (XmlElement)document.GetElementsByTagName("Body", "http://schemas.xmlsoap.org/soap/envelope/")[0]!;

#pragma warning disable CA5350 // verifying what the FACe profile signs with
        var digest = sha1 ? SHA1.HashData(Canonical(document, body)) : SHA256.HashData(Canonical(document, body));
#pragma warning restore CA5350
        if (Convert.ToBase64String(digest) != document.GetElementsByTagName("DigestValue", DsNs)[0]!.InnerText)
            return false;

        var token = document.GetElementsByTagName("BinarySecurityToken", WsseNs)[0]!.InnerText;
        using var certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(token));
        var value = Convert.FromBase64String(document.GetElementsByTagName("SignatureValue", DsNs)[0]!.InnerText);
        return certificate.GetRSAPublicKey()!.VerifyData(
            Canonical(document, signedInfo), value,
            sha1 ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Signed_BodySignatureVerifies(bool sha1)
    {
        Verifies(Load(Signed(sha1))).ShouldBeTrue();
    }

    [Test]
    public void Signed_ChangingTheBody_BreaksTheSignature()
    {
        var document = Load(Signed());
        document.GetElementsByTagName("correo")[0]!.InnerText = "someone-else@example.com";

        Verifies(document).ShouldBeFalse();
    }

    [Test]
    public void Signed_HasTheLayoutOfFacesDocumentation()
    {
        var xml = XDocument.Parse(Signed());
        XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
        XNamespace wsse = WsseNs, wsu = WsuNs, ds = DsNs;

        var security = xml.Root!.Element(soap + "Header")!.Element(wsse + "Security")!;
        security.Attribute(soap + "mustUnderstand")!.Value.ShouldBe("1");
        security.Elements().Select(e => e.Name.LocalName).ShouldBe(["BinarySecurityToken", "Signature", "Timestamp"]);

        var token = security.Element(wsse + "BinarySecurityToken")!;
        token.Attribute("ValueType")!.Value.ShouldEndWith("#X509v3");
        token.Value.ShouldBe(Convert.ToBase64String(_certificate.RawData));

        var signature = security.Element(ds + "Signature")!;
        signature.Element(ds + "SignedInfo")!.Element(ds + "CanonicalizationMethod")!.Attribute("Algorithm")!.Value
            .ShouldBe("http://www.w3.org/2001/10/xml-exc-c14n#");
        var bodyId = xml.Root.Element(soap + "Body")!.Attribute(wsu + "Id")!.Value;
        signature.Descendants(ds + "Reference").Single().Attribute("URI")!.Value.ShouldBe($"#{bodyId}");

        // The key info points at the token
        var tokenId = token.Attribute(wsu + "Id")!.Value;
        signature.Descendants(wsse + "Reference").Single().Attribute("URI")!.Value.ShouldBe($"#{tokenId}");

        var timestamp = security.Element(wsu + "Timestamp")!;
        timestamp.Element(wsu + "Created")!.Value.ShouldBe("2026-10-03T09:30:00.000Z");
        timestamp.Element(wsu + "Expires")!.Value.ShouldBe("2026-10-03T09:35:00.000Z");
    }

    [Test]
    public void Signed_UsesSha256ByDefault_AndSha1OnRequest()
    {
        Signed().ShouldContain("rsa-sha256");
        Signed().ShouldNotContain("rsa-sha1");
        Signed(sha1: true).ShouldContain("#rsa-sha1");
        Signed(sha1: true).ShouldContain("xmldsig#sha1");
    }

    [Test]
    public void Signed_CertificateWithoutAPrivateKey_Throws()
    {
        using var publicOnly = X509CertificateLoader.LoadCertificate(_certificate.Export(X509ContentType.Cert));

        Should.Throw<InvalidOperationException>(() =>
            WsSecuritySigner.SignedEnvelope(FaceSoap.GetInvoice(Staging, "X"), publicOnly, false, Now));
    }

    // --- The answer ------------------------------------------------------------------------------

    private const string Answer = @"<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ns=""https://se-ws-face.redsara.es/proveedores/v1/factura"">
<env:Header/><env:Body><ns:enviarFacturaResponse><return><factura>
  <registro><codigo>REGAGE26e000001</codigo><fecha>2026-10-03T09:31:05+02:00</fecha></registro>
  <importe><valor>8586.59</valor><moneda>EUR</moneda></importe>
  <serie>26</serie><numero>10-001</numero><fechaExpedicion>2026-10-03T00:00:00+02:00</fechaExpedicion>
  <estadoTramitacion><codigo>1200</codigo><nombre>Registrada</nombre></estadoTramitacion>
  <estadoAnulacion><codigo>4100</codigo><nombre>No solicitada anulación</nombre></estadoAnulacion>
  <emisor><nif>12345678Z</nif><nombre>Ana Perez</nombre></emisor><firmante><nif>12345678Z</nif><nombre>Ana Perez</nombre></firmante>
  <receptor><nif>Q2826000H</nif><nombre>Ayuntamiento de Prueba</nombre></receptor>
  <relacion>
    <oficinaContable><codigo>L01280796</codigo><nombre>Oficina</nombre></oficinaContable>
    <organoGestor><codigo>L01280797</codigo><nombre>Organo</nombre></organoGestor>
    <unidadTramitadora><codigo>GE0001234</codigo><nombre>Unidad</nombre></unidadTramitadora>
  </relacion>
  <ficheros><facturae>f1</facturae><anexos/></ficheros>
</factura></return></ns:enviarFacturaResponse></env:Body></env:Envelope>";

    [Test]
    public void ParseInvoiceResponse_ReadsTheRegistryAndTheStatus()
    {
        var invoice = FaceSoap.ParseInvoiceResponse(Answer);

        invoice.RegistryCode.ShouldBe("REGAGE26e000001");
        invoice.RegisteredAt.ShouldBe(new DateTime(2026, 10, 3, 7, 31, 5, DateTimeKind.Utc));
        invoice.Series.ShouldBe("26");
        invoice.Number.ShouldBe("10-001");
        invoice.StatusCode.ShouldBe("1200");
        invoice.StatusName.ShouldBe("Registrada");
        invoice.CancellationCode.ShouldBe("4100");
        invoice.ReceiverName.ShouldBe("Ayuntamiento de Prueba");
        invoice.AccountingOffice.ShouldBe("L01280796");
        invoice.ManagingBody.ShouldBe("L01280797");
        invoice.ProcessingUnit.ShouldBe("GE0001234");
    }

    [Test]
    public void ParseInvoiceResponse_SoapFault_ReportsItsText()
    {
        const string fault = @"<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Body><env:Fault>
<faultcode>env:Server</faultcode><faultstring>El certificado no está dado de alta</faultstring><detail>1062</detail></env:Fault></env:Body></env:Envelope>";

        var ex = Should.Throw<FaceException>(() => FaceSoap.ParseInvoiceResponse(fault));

        ex.Message.ShouldContain("no está dado de alta");
        ex.Message.ShouldContain("1062");
    }

    [Test]
    public void ParseInvoiceResponse_NotXml_IsAnError() =>
        Should.Throw<FaceException>(() => FaceSoap.ParseInvoiceResponse("<html>bad gateway"));

    [Test]
    public void ParseInvoiceResponse_AnswerWithoutAnInvoice_IsAnError() =>
        Should.Throw<FaceException>(() => FaceSoap.ParseInvoiceResponse(
            @"<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Body><x/></env:Body></env:Envelope>"));
}
