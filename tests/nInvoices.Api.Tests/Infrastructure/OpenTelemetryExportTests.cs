using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using nInvoices.Api.Infrastructure;
using Shouldly;

namespace nInvoices.Api.Tests.Infrastructure;

/// <summary>What leaves the server over OTLP: where, with which credentials, and without query strings.</summary>
[TestFixture]
public sealed class OpenTelemetryExportTests
{
    private static OtlpLogSettings? Settings(params (string Key, string Value)[] variables) =>
        OtlpLogSettings.From(
            new ConfigurationBuilder().AddInMemoryCollection(variables.Select(v => KeyValuePair.Create(v.Key, (string?)v.Value))).Build(),
            "nInvoices.Api",
            "Production");

    [Test]
    public void Settings_NoEndpoint_MeansLogsStayLocal()
    {
        Settings().ShouldBeNull();
        Settings(("OTEL_EXPORTER_OTLP_ENDPOINT", "  ")).ShouldBeNull();
    }

    [Test]
    public void Settings_GrafanaCloud_UsesHttpProtobufWithTheLogsPath_AndTheDecodedBasicHeader()
    {
        var settings = Settings(
            ("OTEL_EXPORTER_OTLP_ENDPOINT", "https://otlp-gateway-prod-eu-west-2.grafana.net/otlp/"),
            ("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf"),
            ("OTEL_EXPORTER_OTLP_HEADERS", "Authorization=Basic%20MTIzNDU2OnNlY3JldA=="))!;

        settings.Endpoint.ShouldBe("https://otlp-gateway-prod-eu-west-2.grafana.net/otlp/v1/logs");
        settings.Grpc.ShouldBeFalse();
        settings.Headers["Authorization"].ShouldBe("Basic MTIzNDU2OnNlY3JldA==");
    }

    [Test]
    public void Settings_WithoutAProtocol_IsGrpcOnTheEndpointAsGiven()
    {
        var settings = Settings(("OTEL_EXPORTER_OTLP_ENDPOINT", "http://collector:4317"))!;

        settings.Grpc.ShouldBeTrue();
        settings.Endpoint.ShouldBe("http://collector:4317");
    }

    [Test]
    public void Settings_ALogsEndpoint_IsUsedAsItIs()
    {
        var settings = Settings(
            ("OTEL_EXPORTER_OTLP_ENDPOINT", "https://general.example/otlp"),
            ("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT", "https://logs.example/custom/path"),
            ("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf"))!;

        settings.Endpoint.ShouldBe("https://logs.example/custom/path");
    }

    [Test]
    public void Settings_LogsExporterNone_KeepsLogsLocalEvenWithAnEndpoint()
    {
        Settings(("OTEL_EXPORTER_OTLP_ENDPOINT", "https://x.example/otlp"), ("OTEL_LOGS_EXPORTER", "none")).ShouldBeNull();
    }

    [Test]
    public void Settings_Resource_HasTheServiceAndEnvironment_AndTheOperatorsOverrides()
    {
        var defaults = Settings(("OTEL_EXPORTER_OTLP_ENDPOINT", "http://c:4317"))!;
        var custom = Settings(
            ("OTEL_EXPORTER_OTLP_ENDPOINT", "http://c:4317"),
            ("OTEL_SERVICE_NAME", "ninvoices-api"),
            ("OTEL_RESOURCE_ATTRIBUTES", "service.namespace=ninvoices, deployment.environment=prod"))!;

        defaults.ResourceAttributes["service.name"].ShouldBe("nInvoices.Api");
        defaults.ResourceAttributes["deployment.environment"].ShouldBe("Production");
        custom.ResourceAttributes["service.name"].ShouldBe("ninvoices-api");
        custom.ResourceAttributes["service.namespace"].ShouldBe("ninvoices");
        custom.ResourceAttributes["deployment.environment"].ShouldBe("prod");
    }

    [TestCase("a=1,b=2", 2)]
    [TestCase("a=1,,broken,=x, c = 3 ", 2)]
    [TestCase("", 0)]
    [TestCase(null, 0)]
    public void ParseList_KeepsWellFormedPairsOnly(string? list, int expected)
    {
        OtlpLogSettings.ParseList(list).Count.ShouldBe(expected);
    }

    [Test]
    public void QueryStringStripper_RemovesTheQuery_KeepsThePath()
    {
        using var activity = new Activity("GET api/Gmail/callback");
        activity.SetTag("url.path", "/api/gmail/callback");
        activity.SetTag("url.query", "code=4%2F0AbCd&state=secret");
        activity.SetTag("url.full", "https://oauth2.example/token?code=abc&x=1");
        activity.SetTag("http.route", "api/Gmail/callback");

        new QueryStringStripper().OnEnd(activity);

        activity.GetTagItem("url.query").ShouldBeNull();
        activity.GetTagItem("url.full").ShouldBe("https://oauth2.example/token");
        activity.GetTagItem("url.path").ShouldBe("/api/gmail/callback");
        activity.GetTagItem("http.route").ShouldBe("api/Gmail/callback");
    }
}
