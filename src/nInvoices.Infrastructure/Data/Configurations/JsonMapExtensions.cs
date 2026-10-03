using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace nInvoices.Infrastructure.Data.Configurations;

internal static class JsonMapExtensions
{
    /// <summary>Stores a string map as one JSON object, and tells EF when its content changed.</summary>
    public static PropertyBuilder<Dictionary<string, string>> HasJsonMapConversion(this PropertyBuilder<Dictionary<string, string>> property)
    {
        return property
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>(),
                new ValueComparer<Dictionary<string, string>>(
                    (a, b) => a != null && b != null && a.Count == b.Count && !a.Except(b).Any(),
                    v => v.Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key, kv.Value)),
                    v => new Dictionary<string, string>(v)))
            .IsRequired();
    }
}
