using System.Net;

namespace nInvoices.Api.Infrastructure;

/// <summary>
/// Custom HTTP handler that rewrites requests to use keycloak:8080 for internal Docker communication.
/// Keycloak returns URLs using its configured hostname in OIDC metadata,
/// but from inside Docker, those need to be routed to the keycloak container directly.
/// Hosts to rewrite are provided via configuration.
/// </summary>
public sealed class KeycloakBackchannelHandler : HttpClientHandler
{
    private readonly string[] _hostsToRewrite;

    public KeycloakBackchannelHandler(IEnumerable<string>? additionalHosts = null)
    {
        var hosts = new List<string> { "localhost" };
        if (additionalHosts is not null)
            hosts.AddRange(additionalHosts.Where(h => !string.IsNullOrWhiteSpace(h)));
        _hostsToRewrite = [.. hosts];
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is not null 
            && request.RequestUri.Port == 8080
            && _hostsToRewrite.Contains(request.RequestUri.Host, StringComparer.OrdinalIgnoreCase))
        {
            var builder = new UriBuilder(request.RequestUri)
            {
                Host = "keycloak"
            };
            request.RequestUri = builder.Uri;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
