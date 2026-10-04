using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using nInvoices.Application.Compliance.Spain.Face;
using nInvoices.Core.Configuration;

namespace nInvoices.Infrastructure.Face;

/// <summary>
/// Talks to FACe providers web service: a SOAP 1.1 request over TLS whose body is signed (WS-Security) with
/// the user certificate, the one the user registered in FACe.
/// </summary>
public sealed class FaceClient : IFaceClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly FaceOptions _options;
    private readonly TimeProvider _time;

    /// <param name="http">Tests pass a client with a stub handler.</param>
    public FaceClient(IOptions<FaceOptions> options, TimeProvider time, HttpClient? http = null)
    {
        _http = http ?? new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(1) }) { Timeout = Timeout };
        _options = options.Value;
        _time = time;
    }

    public async Task<FaceInvoice> SendInvoiceAsync(
        FaceTarget target, X509Certificate2 certificate, string notificationEmail, string fileName, byte[] xsig,
        CancellationToken cancellationToken = default) =>
        FaceSoap.ParseInvoiceResponse(await PostAsync(
            target, certificate, "enviarFactura", FaceSoap.SendInvoice(target, notificationEmail, fileName, xsig), cancellationToken));

    public async Task<FaceInvoice> GetInvoiceAsync(
        FaceTarget target, X509Certificate2 certificate, string registryCode, CancellationToken cancellationToken = default) =>
        FaceSoap.ParseInvoiceResponse(await PostAsync(
            target, certificate, "detalleFactura", FaceSoap.GetInvoice(target, registryCode), cancellationToken));

    private async Task<string> PostAsync(
        FaceTarget target, X509Certificate2 certificate, string operation, XElement body, CancellationToken cancellationToken)
    {
        var envelope = WsSecuritySigner.SignedEnvelope(body, certificate, _options.UseSha1, _time.GetUtcNow().UtcDateTime);
        var address = string.IsNullOrWhiteSpace(_options.ServiceUrl) ? target.Url : _options.ServiceUrl;

        using var request = new HttpRequestMessage(HttpMethod.Post, address) { Content = new StringContent(envelope, Encoding.UTF8) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
        request.Headers.TryAddWithoutValidation("SOAPAction", FaceSoap.SoapAction(target, operation));

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            var answer = await response.Content.ReadAsStringAsync(cancellationToken);

            // A SOAP fault comes with an error status and says more than the status does
            if (!response.IsSuccessStatusCode && !answer.Contains("Fault", StringComparison.Ordinal))
                throw new FaceException($"FACe answered HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            return answer;
        }
        catch (HttpRequestException ex)
        {
            throw new FaceException($"FACe could not be reached: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FaceException("FACe did not answer in time", ex);
        }
    }
}

public static class FaceExtensions
{
    /// <summary>Registers the FACe client. It is only used when the user sends an invoice or asks for its status.</summary>
    public static IServiceCollection AddFace(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IFaceClient>(sp =>
            new FaceClient(sp.GetRequiredService<IOptions<FaceOptions>>(), sp.GetRequiredService<TimeProvider>()));
        return services;
    }
}
