using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using nInvoices.Application.Compliance.Spain.Verifactu;
using nInvoices.Infrastructure.Verifactu;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Verifactu;

[TestFixture]
public sealed class AeatVerifactuClientTests
{
    private const string Tik = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";
    private const string TikR = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/RespuestaSuministro.xsd";

    private static readonly XElement Batch = VerifactuXml.Batch("Ana", "12345678Z", []);
    private static readonly AeatTarget Test = new(false, false);

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private static string Accepted(string number = "26-10-001") => $@"<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Body>
<tikR:RespuestaRegFactuSistemaFacturacion xmlns:tikR=""{TikR}"" xmlns:tik=""{Tik}"">
  <tikR:CSV>A-1</tikR:CSV><tikR:TiempoEsperaEnvio>60</tikR:TiempoEsperaEnvio><tikR:EstadoEnvio>Correcto</tikR:EstadoEnvio>
  <tikR:RespuestaLinea><tikR:IDFactura><tik:IDEmisorFactura>12345678Z</tik:IDEmisorFactura><tik:NumSerieFactura>{number}</tik:NumSerieFactura><tik:FechaExpedicionFactura>03-10-2026</tik:FechaExpedicionFactura></tikR:IDFactura>
  <tikR:Operacion><tik:TipoOperacion>Alta</tik:TipoOperacion></tikR:Operacion><tikR:EstadoRegistro>Correcto</tikR:EstadoRegistro></tikR:RespuestaLinea>
</tikR:RespuestaRegFactuSistemaFacturacion></env:Body></env:Envelope>";

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return await respond(request);
        }
    }

    private static HttpResponseMessage Xml(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "text/xml") };

    private static X509Certificate2 NewCertificate(string subject, bool server = false)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (server)
        {
            var names = new SubjectAlternativeNameBuilder();
            names.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build());
        }

        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx, "pw"), "pw", X509KeyStorageFlags.Exportable);
    }

    // --- The request ----------------------------------------------------------------------------------

    [Test]
    public async Task Send_PostsASoap11EnvelopeToTheServiceAddress()
    {
        using var certificate = NewCertificate("CN=User");
        var handler = new StubHandler(_ => Task.FromResult(Xml(HttpStatusCode.OK, Accepted())));
        var client = new AeatVerifactuClient(_ => handler);

        var answer = await client.SendAsync(Test, certificate, Batch, Token);

        var (request, body) = handler.Seen.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.ToString().ShouldBe("https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP");
        request.Content!.Headers.ContentType!.ToString().ShouldBe("text/xml; charset=utf-8");
        request.Headers.GetValues("SOAPAction").Single().ShouldBe("\"\"");
        XDocument.Parse(body).Root!.Elements().Last().Elements().Single().Name.ShouldBe(VerifactuXml.Lr + "RegFactuSistemaFacturacion");

        answer.Csv.ShouldBe("A-1");
        answer.Lines.Single().Status.ShouldBe("Correcto");
    }

    [Test]
    public async Task Send_HandlerIsBuiltForTheCertificateOfTheUser()
    {
        using var certificate = NewCertificate("CN=User");
        X509Certificate2? given = null;
        var client = new AeatVerifactuClient(c =>
        {
            given = c;
            return new StubHandler(_ => Task.FromResult(Xml(HttpStatusCode.OK, Accepted())));
        });

        await client.SendAsync(Test, certificate, Batch, Token);

        given!.Thumbprint.ShouldBe(certificate.Thumbprint);
    }

    // --- What goes wrong --------------------------------------------------------------------------------

    [Test]
    public async Task Send_SoapFaultWithAnErrorStatus_ReportsTheFault()
    {
        using var certificate = NewCertificate("CN=User");
        var fault = @"<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Body><env:Fault><faultcode>env:Client</faultcode><faultstring>Codigo[4102].El XML no cumple el esquema.</faultstring></env:Fault></env:Body></env:Envelope>";
        var client = new AeatVerifactuClient(_ => new StubHandler(_ => Task.FromResult(Xml(HttpStatusCode.InternalServerError, fault))));

        var ex = await Should.ThrowAsync<AeatException>(() => client.SendAsync(Test, certificate, Batch, Token));

        ex.Message.ShouldContain("4102");
    }

    [Test]
    public async Task Send_AnErrorPageInsteadOfSoap_ReportsTheStatus()
    {
        using var certificate = NewCertificate("CN=User");
        var client = new AeatVerifactuClient(_ => new StubHandler(_ => Task.FromResult(Xml(HttpStatusCode.ServiceUnavailable, "<html>down</html>"))));

        var ex = await Should.ThrowAsync<AeatException>(() => client.SendAsync(Test, certificate, Batch, Token));

        ex.Message.ShouldContain("503");
    }

    [Test]
    public async Task Send_ConnectionFailure_IsReportedAsUnreachable()
    {
        using var certificate = NewCertificate("CN=User");
        var client = new AeatVerifactuClient(_ => new StubHandler(_ => throw new HttpRequestException("connection refused")));

        var ex = await Should.ThrowAsync<AeatException>(() => client.SendAsync(Test, certificate, Batch, Token));

        ex.Message.ShouldContain("could not be reached");
    }

    [Test]
    public async Task Send_Timeout_IsReported()
    {
        using var certificate = NewCertificate("CN=User");
        var client = new AeatVerifactuClient(_ => new StubHandler(_ => throw new TaskCanceledException("timed out")));

        var ex = await Should.ThrowAsync<AeatException>(() => client.SendAsync(Test, certificate, Batch, Token));

        ex.Message.ShouldContain("in time");
    }

    // --- Mutual TLS: the real handler against a server that asks for the client certificate ---------

    [Test]
    public void DefaultHandler_PresentsTheCertificate()
    {
        using var certificate = NewCertificate("CN=User");

        using var handler = AeatVerifactuClient.DefaultHandler(certificate);

        handler.SslOptions.ClientCertificates.ShouldNotBeNull();
        handler.SslOptions.ClientCertificates!.Cast<X509Certificate>().Single().GetCertHashString().ShouldBe(certificate.GetCertHashString());
    }

    [Test]
    public async Task Send_OverRealTls_AuthenticatesWithTheUserCertificate()
    {
        using var serverCertificate = NewCertificate("CN=localhost", server: true);
        using var userCertificate = NewCertificate("CN=Ana Perez, O=Test");
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        string? seenThumbprint = null;
        string? seenRequestLine = null;

        var server = Task.Run(async () =>
        {
            using var tcp = await listener.AcceptTcpClientAsync(Token);
            await using var ssl = new SslStream(tcp.GetStream(), false, (_, certificate, _, _) =>
            {
                seenThumbprint = (certificate as X509Certificate2 ?? (certificate is null ? null : new X509Certificate2(certificate)))?.Thumbprint;
                return certificate is not null; // like AEAT: a client without a certificate is turned away
            });
            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = serverCertificate,
                ClientCertificateRequired = true,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, Token);

            // Read the request: headers, then the body its Content-Length announces
            var buffer = new List<byte>();
            var one = new byte[1];
            while (!(buffer.Count >= 4 && buffer[^4] == '\r' && buffer[^3] == '\n' && buffer[^2] == '\r' && buffer[^1] == '\n'))
            {
                if (await ssl.ReadAsync(one, Token) == 0) break;
                buffer.Add(one[0]);
            }

            var head = Encoding.ASCII.GetString(buffer.ToArray());
            seenRequestLine = head.Split("\r\n")[0];
            var length = int.Parse(head.Split("\r\n").First(l => l.StartsWith("content-length", StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim());
            var body = new byte[length];
            var read = 0;
            while (read < length)
                read += await ssl.ReadAsync(body.AsMemory(read), Token);

            var reply = Encoding.UTF8.GetBytes(Accepted());
            await ssl.WriteAsync(Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: text/xml; charset=utf-8\r\nContent-Length: {reply.Length}\r\nConnection: close\r\n\r\n"), Token);
            await ssl.WriteAsync(reply, Token);
            await ssl.FlushAsync(Token);
        }, Token);

        // The real default handler, trusting only this test's self-signed server
        var client = new AeatVerifactuClient(
            certificate =>
            {
                var handler = AeatVerifactuClient.DefaultHandler(certificate);
                handler.SslOptions.RemoteCertificateValidationCallback = (_, remote, _, _) => remote?.GetCertHashString() == serverCertificate.GetCertHashString();
                return handler;
            },
            _ => new Uri($"https://127.0.0.1:{port}/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP"));

        var answer = await client.SendAsync(Test, userCertificate, Batch, Token);
        await server;
        listener.Stop();

        seenThumbprint.ShouldBe(userCertificate.Thumbprint);
        seenRequestLine.ShouldBe("POST /wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP HTTP/1.1");
        answer.Csv.ShouldBe("A-1");
    }
}
