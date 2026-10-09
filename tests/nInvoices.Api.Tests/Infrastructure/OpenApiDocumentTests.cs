using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using nInvoices.Api.Controllers;
using nInvoices.Api.Infrastructure;
using nInvoices.Application.DTOs;
using nInvoices.Core.Enums;
using Shouldly;

namespace nInvoices.Api.Tests.Infrastructure;

/// <summary>
/// The OpenAPI document must describe what the API really sends, or clients generated from it (the
/// web app's types) are wrong: enums by name, numbers as numbers, files as binary, required fields
/// marked. Built from the controllers with the app's own JSON and document setup.
/// </summary>
[TestFixture]
public sealed class OpenApiDocumentTests
{
    private WebApplication _app = null!;
    private JsonElement _document;
    private JsonElement _schemas;
    private string _json = "";

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [OneTimeSetUp]
    public async Task GenerateDocument()
    {
        // Named as the API, so the document is the one the API serves
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(InvoicesController).Assembly.GetName().Name
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(InvoicesController).Assembly).AddApiJson();
        builder.Services.AddApiDocument();

        _app = builder.Build();
        _app.MapControllers();
        _app.MapOpenApi();
        await _app.StartAsync(Token);

        var json = await _app.GetTestClient().GetStringAsync("/openapi/v1.json", Token);
        _json = json;
        _document = JsonDocument.Parse(json).RootElement.Clone();
        _schemas = _document.GetProperty("components").GetProperty("schemas");
    }

    [OneTimeTearDown]
    public async Task StopApp() => await _app.DisposeAsync();

    private static string[] Types(JsonElement schema) =>
        !schema.TryGetProperty("type", out var type) ? []
        : type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().Select(t => t.GetString()!).ToArray()
        : [type.GetString()!];

    private IEnumerable<(string Schema, string Property, JsonElement Value)> Properties() =>
        from schema in _schemas.EnumerateObject()
        where schema.Value.TryGetProperty("properties", out _)
        from property in schema.Value.GetProperty("properties").EnumerateObject()
        select (schema.Name, property.Name, property.Value);

    /// <summary>
    /// The web app generates its API types from a copy of this document (src/nInvoices.Web/openapi.json,
    /// then <c>npm run types</c>): the copy must be the document the API serves now. Refresh it with
    /// <c>UPDATE_OPENAPI_SNAPSHOT=1 dotnet test --filter OpenApiDocumentTests</c>.
    /// </summary>
    [Test]
    public async Task Snapshot_InTheWebApp_IsTheCurrentDocument()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "nInvoices.Web", "openapi.json");
        var current = Snapshot(_json);

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI_SNAPSHOT") == "1")
        {
            await File.WriteAllTextAsync(path, current, Token);
            Assert.Pass($"Snapshot written to {path}");
        }

        var saved = File.Exists(path) ? (await File.ReadAllTextAsync(path, Token)).ReplaceLineEndings("\n") : null;
        (saved == current).ShouldBeTrue(
            "The API changed: refresh src/nInvoices.Web/openapi.json (UPDATE_OPENAPI_SNAPSHOT=1 dotnet test " +
            "--filter OpenApiDocumentTests), then run npm run types in src/nInvoices.Web and fix what vue-tsc reports");
    }

    /// <summary>The document as committed: indented, without the request-dependent server URL.</summary>
    private static string Snapshot(string json)
    {
        var document = JsonNode.Parse(json)!.AsObject();
        document.Remove("servers");
        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n";
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nInvoices.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("nInvoices.slnx not found above the test directory");
    }

    [Test]
    public void Numbers_AreDocumentedAsNumbers_NotAsNumbersOrStrings()
    {
        var loose = Properties()
            .Where(p => Types(p.Value) is var t && (t.Contains("integer") || t.Contains("number")) && t.Contains("string"))
            .Select(p => $"{p.Schema}.{p.Property}")
            .ToList();

        loose.ShouldBeEmpty();
    }

    [TestCase(typeof(InvoiceStatus))]
    [TestCase(typeof(InvoiceType))]
    [TestCase(typeof(DayType))]
    [TestCase(typeof(RateType))]
    [TestCase(typeof(TaxApplicationType))]
    public void Enums_AreStrings_ListingTheirNames(Type enumType)
    {
        var schema = _schemas.GetProperty(enumType.Name);

        Types(schema).ShouldBe(["string"]);
        schema.GetProperty("enum").EnumerateArray().Select(v => v.GetString()).ShouldBe(Enum.GetNames(enumType));
    }

    [Test]
    public void FrameworkTypes_DoNotLeakIntoTheSchemas()
    {
        var names = _schemas.EnumerateObject().Select(s => s.Name).ToList();

        names.ShouldNotContain("FileContentResult");
        names.ShouldNotContain("EntityTagHeaderValue");
        names.ShouldNotContain("StringSegment");
    }

    [TestCase("/api/Invoices/{id}/pdf", "get", "application/pdf")]
    [TestCase("/api/Invoices/{id}/calendar/pdf", "get", "application/pdf")]
    [TestCase("/api/Invoices/{id}/monthlyreport/pdf", "get", "application/pdf")]
    [TestCase("/api/Invoices/bulk/pdf", "post", "application/zip")]
    public void FileDownloads_AreBinary_OfTheirContentType(string path, string method, string contentType)
    {
        var content = _document.GetProperty("paths").GetProperty(path).GetProperty(method)
            .GetProperty("responses").GetProperty("200").GetProperty("content");

        content.EnumerateObject().Select(c => c.Name).ShouldBe([contentType]);
        var schema = content.GetProperty(contentType).GetProperty("schema");
        if (schema.TryGetProperty("$ref", out var reference))
            schema = _schemas.GetProperty(reference.GetString()!.Split('/')[^1]);
        Types(schema).ShouldBe(["string"]);
        schema.GetProperty("format").GetString().ShouldBe("binary");
    }

    [Test]
    public void AnInvoiceAsTheControllersWriteIt_MatchesItsSchema()
    {
        var mvcJson = _app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
        var money = new MoneyDto(1220m, "EUR");
        var invoice = new InvoiceDto
        {
            Id = 7, CustomerId = 1, Type = InvoiceType.Monthly, InvoiceNumber = "26-10-001", IssueDate = new DateOnly(2026, 10, 9),
            Subtotal = money, TotalExpenses = money, TotalTaxes = money, Total = money, Status = InvoiceStatus.Finalized,
            CreatedAt = new DateTime(2026, 10, 9, 9, 0, 0, DateTimeKind.Utc)
        };
        var written = JsonDocument.Parse(JsonSerializer.Serialize(invoice, mvcJson)).RootElement;
        var schema = _schemas.GetProperty(nameof(InvoiceDto));
        var properties = schema.GetProperty("properties");

        foreach (var required in schema.GetProperty("required").EnumerateArray().Select(r => r.GetString()!))
            written.TryGetProperty(required, out _).ShouldBeTrue($"{required} is required but not written");
        foreach (var field in written.EnumerateObject())
        {
            properties.TryGetProperty(field.Name, out var documented).ShouldBeTrue($"{field.Name} is written but not documented");
            if (documented.TryGetProperty("$ref", out var reference) &&
                _schemas.GetProperty(reference.GetString()!.Split('/')[^1]).TryGetProperty("enum", out var values))
                values.EnumerateArray().Select(v => v.GetString()).ShouldContain(field.Value.GetString());
        }
        written.GetProperty("status").GetString().ShouldBe("Finalized");
        written.GetProperty("total").GetProperty("amount").ValueKind.ShouldBe(JsonValueKind.Number);
    }
}
