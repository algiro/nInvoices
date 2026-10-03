using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using nInvoices.Application.Compliance.Spain.Verifactu;

namespace nInvoices.Infrastructure.Verifactu;

/// <summary>
/// Talks to the Tax Agency Verifactu web service: a SOAP 1.1 request over TLS in which the client
/// authenticates with the user electronic certificate (mutual TLS).
/// </summary>
public sealed class AeatVerifactuClient : IAeatVerifactuClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly Func<X509Certificate2, HttpMessageHandler> _handlerFactory;
    private readonly Func<AeatTarget, Uri> _addressOf;

    /// <param name="handlerFactory">Builds the HTTP handler that presents the certificate; tests pass their own.</param>
    /// <param name="addressOf">Where a target is; tests point it at a local server.</param>
    public AeatVerifactuClient(Func<X509Certificate2, HttpMessageHandler>? handlerFactory = null, Func<AeatTarget, Uri>? addressOf = null)
    {
        _handlerFactory = handlerFactory ?? DefaultHandler;
        _addressOf = addressOf ?? (target => new Uri(target.Url));
    }

    public async Task<AeatResponse> SendAsync(
        AeatTarget target, X509Certificate2 certificate, XElement batch, CancellationToken cancellationToken = default)
    {
        var envelope = AeatSoap.Envelope(batch).ToString(SaveOptions.DisableFormatting);

        using var request = new HttpRequestMessage(HttpMethod.Post, _addressOf(target))
        {
            Content = new StringContent(envelope, Encoding.UTF8)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
        request.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

        try
        {
            // One handler per submission: it carries this user certificate and nobody else's
            using var http = new HttpClient(_handlerFactory(certificate), disposeHandler: true) { Timeout = Timeout };
            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            // A SOAP fault comes with an error status, and says more than the status does
            try
            {
                return AeatSoap.ParseResponse(body);
            }
            catch (AeatException) when (!response.IsSuccessStatusCode && !body.Contains("Fault", StringComparison.Ordinal))
            {
                throw new AeatException($"The Tax Agency answered HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new AeatException($"The Tax Agency could not be reached: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AeatException("The Tax Agency did not answer in time", ex);
        }
    }

    internal static SocketsHttpHandler DefaultHandler(X509Certificate2 certificate)
    {
        var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(1) };
        handler.SslOptions.ClientCertificates = [certificate];
        return handler;
    }
}
