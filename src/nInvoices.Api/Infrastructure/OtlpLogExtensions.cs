using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace nInvoices.Api.Infrastructure;

/// <summary>
/// Sends Serilog's logs over OTLP to the same place traces and metrics go (ServiceDefaults), configured with
/// the standard OpenTelemetry variables, so a log line arrives with its trace id. Off until an endpoint is set.
/// </summary>
public static class OtlpLogExtensions
{
    /// <summary>
    /// Adds the OTLP sink when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (or <c>OTEL_EXPORTER_OTLP_LOGS_ENDPOINT</c>) is set.
    /// Also read: <c>OTEL_EXPORTER_OTLP_PROTOCOL</c> / <c>_HEADERS</c> (each with a <c>_LOGS_</c> variant),
    /// <c>OTEL_SERVICE_NAME</c>, <c>OTEL_RESOURCE_ATTRIBUTES</c> and <c>OTEL_LOGS_EXPORTER=none</c> to keep logs local.
    /// </summary>
    public static LoggerConfiguration WriteToOtlpWhenConfigured(
        this LoggerConfiguration logger,
        IConfiguration configuration,
        string applicationName,
        string environmentName)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var settings = OtlpLogSettings.From(configuration, applicationName, environmentName);
        if (settings is null)
            return logger;

        return logger.WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = settings.Endpoint;
            options.Protocol = settings.Grpc ? OtlpProtocol.Grpc : OtlpProtocol.HttpProtobuf;
            foreach (var (key, value) in settings.Headers)
                options.Headers[key] = value;
            foreach (var (key, value) in settings.ResourceAttributes)
                options.ResourceAttributes[key] = value;
        });
    }
}

/// <summary>What the OTLP log sink is configured with, worked out from the standard OpenTelemetry variables.</summary>
public sealed record OtlpLogSettings(
    string Endpoint,
    bool Grpc,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, object> ResourceAttributes)
{
    /// <returns>The settings, or null when no endpoint is configured (logs stay on the console and in files).</returns>
    public static OtlpLogSettings? From(IConfiguration configuration, string applicationName, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // The standard switch: traces and metrics can go out while logs stay on the server
        if (string.Equals(Clean(configuration["OTEL_LOGS_EXPORTER"]), "none", StringComparison.OrdinalIgnoreCase))
            return null;

        var logsEndpoint = Clean(configuration["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"]);
        var endpoint = Clean(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        if (logsEndpoint is null && endpoint is null)
            return null;

        // The .NET OpenTelemetry SDK, like the spec, defaults to gRPC; HTTP/protobuf is asked for explicitly
        var protocol = Clean(configuration["OTEL_EXPORTER_OTLP_LOGS_PROTOCOL"])
            ?? Clean(configuration["OTEL_EXPORTER_OTLP_PROTOCOL"])
            ?? "grpc";
        var grpc = !string.Equals(protocol, "http/protobuf", StringComparison.OrdinalIgnoreCase);

        // A signal's own endpoint is used as it is; the general one gets the signal's path over HTTP
        var url = logsEndpoint ?? (grpc ? endpoint! : endpoint!.TrimEnd('/') + "/v1/logs");

        var headers = ParseList(Clean(configuration["OTEL_EXPORTER_OTLP_LOGS_HEADERS"]) ?? Clean(configuration["OTEL_EXPORTER_OTLP_HEADERS"]));

        var resource = new Dictionary<string, object>
        {
            ["service.name"] = Clean(configuration["OTEL_SERVICE_NAME"]) ?? applicationName,
            ["deployment.environment"] = environmentName
        };
        foreach (var (key, value) in ParseList(Clean(configuration["OTEL_RESOURCE_ATTRIBUTES"])))
            resource[key] = value;

        return new OtlpLogSettings(url, grpc, headers, resource);
    }

    /// <summary>
    /// "key=value,key2=value2" as OpenTelemetry writes lists. Values are percent-decoded, so a header with a space
    /// (<c>Authorization=Basic%20abc</c>) can be written in an environment variable.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseList(string? list)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in (list ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
                continue;

            var key = pair[..separator].Trim();
            var value = pair[(separator + 1)..].Trim();
            result[key] = Uri.UnescapeDataString(value);
        }
        return result;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
