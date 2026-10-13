using System.Diagnostics;
using OpenTelemetry;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Removes query strings from the spans before they are exported. A request's query can hold what must not
/// reach a third-party backend: the authorization <c>code</c> of the Gmail connection, or the text searched
/// in the invoice list (customer names). The path stays, so a trace still says which endpoint was called.
/// </summary>
public sealed class QueryStringStripper : BaseProcessor<Activity>
{
    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        // ASP.NET Core (server) and HttpClient (outgoing calls) put the query here
        data.SetTag("url.query", null);

        foreach (var key in new[] { "url.full", "http.url" })
        {
            if (data.GetTagItem(key) is string url && url.IndexOf('?', StringComparison.Ordinal) is var at and >= 0)
                data.SetTag(key, url[..at]);
        }
    }
}
