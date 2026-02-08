using System.Net;

namespace nInvoices.Api.Infrastructure;

/// <summary>
/// Custom HTTP handler that rewrites requests to localhost:8080 to use keycloak:8080 instead.
/// This solves the Docker networking issue where tokens have issuer=localhost:8080
/// but the API container needs to reach Keycloak at keycloak:8080
/// </summary>
public class KeycloakBackchannelHandler : HttpClientHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Rewrite localhost:8080 to keycloak:8080 for internal Docker communication
        if (request.RequestUri?.Host == "localhost" && request.RequestUri.Port == 8080)
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
