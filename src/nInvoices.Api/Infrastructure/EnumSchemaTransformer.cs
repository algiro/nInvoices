using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace nInvoices.Api.Infrastructure;

/// <summary>
/// Enums travel by name (<see cref="ApiJsonExtensions"/>): their schemas list the names, and this
/// says they are strings, so a client generator makes string unions rather than guessing.
/// </summary>
public sealed class EnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Enum is { Count: > 0 } && schema.Type is null)
            schema.Type = JsonSchemaType.String;
        return Task.CompletedTask;
    }
}
