using System.Net;

namespace nInvoices.Api.Infrastructure;

/// <summary>
/// Custom HTTP handler that rewrites requests to use keycloak:8080 for internal Docker communication.
/// Keycloak returns URLs using its configured hostname (DOMAIN_PLACEHOLDER:8080) in OIDC metadata,
/// but from inside Docker, those need to be routed to the keycloak container directly.
/// </summary>
public class KeycloakBackchannelHandler : HttpClientHandler
{
    private static readonly string[] HostsToRewrite = ["localhost", "DOMAIN_PLACEHOLDER"];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is not null 
            && request.RequestUri.Port == 8080
            && HostsToRewrite.Contains(request.RequestUri.Host, StringComparer.OrdinalIgnoreCase))
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
