using System.Text.Json;
using System.Text.Json.Serialization;

namespace nInvoices.Api.Infrastructure;

/// <summary>
/// The API's JSON conventions, applied both where controllers read and write JSON and where the
/// OpenAPI document gets its schemas from (the minimal-API JSON options, a separate set): the
/// document describes what is actually on the wire.
/// </summary>
public static class ApiJsonExtensions
{
    /// <summary>Enums travel by name ("Draft", "Monthly"), as the web app expects.</summary>
    private static void Configure(JsonSerializerOptions options) =>
        options.Converters.Add(new JsonStringEnumConverter());

    public static IMvcBuilder AddApiJson(this IMvcBuilder mvc)
    {
        mvc.AddJsonOptions(options => Configure(options.JsonSerializerOptions));

        mvc.Services.ConfigureHttpJsonOptions(options =>
        {
            Configure(options.SerializerOptions);
            // Numbers are documented as numbers: that is what the API sends and what clients should
            // send. Controllers still accept a number written as a string ("8"), as before.
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });

        return mvc;
    }

    /// <summary>The OpenAPI document (served at /openapi/v1.json in Development), described as <see cref="AddApiJson"/> writes JSON.</summary>
    public static IServiceCollection AddApiDocument(this IServiceCollection services) =>
        services.AddOpenApi(options => options.AddSchemaTransformer<EnumSchemaTransformer>());
}
