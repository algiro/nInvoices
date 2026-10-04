using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using nInvoices.Application.Compliance.Spain.Face;
using nInvoices.Core.Configuration;
using nInvoices.Infrastructure.Face;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Face;

[TestFixture]
public sealed class FaceClientTests
{
    private static readonly FaceTarget Staging = new(false);
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 30, 0, TimeSpan.Zero);

    private X509Certificate2 _certificate = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return await respond(request);
        }
    }

    private const string Answer = @"<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Body><ns:enviarFacturaResponse xmlns:ns=""x""><return><factura>
<registro><codigo>REG1</codigo><fecha>2026-10-03T09:31:05+02:00</fecha></registro>
<estadoTramitacion><codigo>1200</codigo><nombre>Registrada</nombre></estadoTramitacion>
</factura></return></ns:enviarFacturaResponse></env:Body></env:Envelope>";

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Ana, C=ES", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(Now.AddDays(-1), Now.AddYears(1));
        _certificate = X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx, "pw"), "pw", X509KeyStorageFlags.Exportable);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _certificate.Dispose();

    private static FaceClient ClientFor(StubHandler handler, FaceOptions? options = null) =>
        new(Options.Create(options ?? new FaceOptions { Environment = "Test" }), new FixedTime(Now), new HttpClient(handler));

    private static HttpResponseMessage Xml(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "text/xml") };

    [Test]
    public async Task SendInvoice_PostsTheSignedEnvelope_ToTheStagingService()
    {
        var handler = new StubHandler(_ => Task.FromResult(Xml(HttpStatusCode.OK, Answer)));

        var invoice = await ClientFor(handler).SendInvoiceAsync(Staging, _certificate, "ana@example.com", "a.xsig", [1, 2, 3], Token);

        invoice.RegistryCode.ShouldBe("REG1");
        invoice.StatusCode.ShouldBe("1200");

        var (request, body) = handler.Seen.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.ToString().ShouldBe("https://se-ws-face.redsara.es/proveedores/v1/factura");
        request.Content!.Headers.ContentType!.MediaType.ShouldBe("text/xml");
        request.Headers.GetValues("SOAPAction").Single().ShouldBe("\"https://se-ws-face.redsara.es/proveedores/v1/factura#enviarFactura\"");

        var envelope = XDocument.Parse(body);
        envelope.Descendants().Count(e => e.Name.LocalName == "Signature").ShouldBe(1);
        envelope.Descendants().Single(e => e.Name.LocalName == "Created").Value.ShouldBe("2026-10-03T09:30:00.000Z");
        envelope.Descendants().Single(e => e.Name.LocalName == "correo").Value.ShouldBe("ana@example.com");
    }

    [Test]
    public async Task GetInvoice_AsksDetalleFactura()
    {
        var handler = new StubHandler(_ => Task.FromResult(Xml(HttpStatusCode.OK, Answer)));

        await ClientFor(handler).GetInvoiceAsync(Staging, _certificate, "REG1", Token);

        var (request, body) = handler.Seen.ShouldHaveSingleItem();
        request.Headers.GetValues("SOAPAction").Single().ShouldEndWith("#detalleFactura\"");
        body.ShouldContain("REG1");
    }

    [Test]
    public async Task ServiceUrlOverride_SendsThereInstead()
    {
        var handler = new StubHandler(_ => Task.FromResult(Xml(HttpStatusCode.OK, Answer)));
        var client = ClientFor(handler, new FaceOptions { Environment = "Test", ServiceUrl = "http://localhost:5999/face" });

        await client.SendInvoiceAsync(Staging, _certificate, "a@b.es", "a.xsig", [1], Token);

        handler.Seen.Single().Request.RequestUri!.ToString().ShouldBe("http://localhost:5999/face");
    }

    [Test]
    public async Task Sha1Option_SignsWithSha1()
    {
        var handler = new StubHandler(_ => Task.FromResult(Xml(HttpStatusCode.OK, Answer)));

        await ClientFor(handler, new FaceOptions { Environment = "Test", SignatureAlgorithm = "Sha1" })
            .SendInvoiceAsync(Staging, _certificate, "a@b.es", "a.xsig", [1], Token);

        handler.Seen.Single().Body.ShouldContain("#rsa-sha1");
    }

    [Test]
    public async Task SoapFaultWithErrorStatus_KeepsTheFaultText()
    {
        var handler = new StubHandler(_ => Task.FromResult(Xml(HttpStatusCode.InternalServerError,
            @"<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Body><env:Fault><faultcode>env:Server</faultcode><faultstring>Certificado no registrado</faultstring></env:Fault></env:Body></env:Envelope>")));

        var ex = await Should.ThrowAsync<FaceException>(() => ClientFor(handler).SendInvoiceAsync(Staging, _certificate, "a@b.es", "a.xsig", [1], Token));

        ex.Message.ShouldContain("Certificado no registrado");
    }

    [Test]
    public async Task ErrorStatusWithoutAFault_ReportsTheStatus()
    {
        var handler = new StubHandler(_ => Task.FromResult(Xml(HttpStatusCode.BadGateway, "<html>bad gateway</html>")));

        var ex = await Should.ThrowAsync<FaceException>(() => ClientFor(handler).SendInvoiceAsync(Staging, _certificate, "a@b.es", "a.xsig", [1], Token));

        ex.Message.ShouldContain("502");
    }

    [Test]
    public async Task Unreachable_IsAFaceException()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));

        var ex = await Should.ThrowAsync<FaceException>(() => ClientFor(handler).GetInvoiceAsync(Staging, _certificate, "REG1", Token));

        ex.Message.ShouldContain("could not be reached");
    }

    [Test]
    public async Task Timeout_IsAFaceException()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException("timed out"));

        var ex = await Should.ThrowAsync<FaceException>(() => ClientFor(handler).GetInvoiceAsync(Staging, _certificate, "REG1", Token));

        ex.Message.ShouldContain("in time");
    }
}
