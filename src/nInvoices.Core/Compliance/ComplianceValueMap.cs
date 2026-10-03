namespace nInvoices.Core.Compliance;

/// <summary>
/// Helpers for the "COUNTRY.field" maps that carry a country extra data on entities (customers,
/// taxes): one map holds the values of every country.
/// </summary>
public static class ComplianceValueMap
{
    /// <summary>One country values, without the country prefix.</summary>
    public static IReadOnlyDictionary<string, string> For(IReadOnlyDictionary<string, string> map, string countryCode)
    {
        var prefix = countryCode.ToUpperInvariant() + ".";
        return map
            .Where(v => v.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(v => v.Key[prefix.Length..], v => v.Value);
    }

    /// <summary>A new map with one country values replaced (the others kept). Blank values are dropped.</summary>
    public static Dictionary<string, string> Replace(
        IReadOnlyDictionary<string, string> map, string countryCode, IReadOnlyDictionary<string, string> values)
    {
        var prefix = countryCode.ToUpperInvariant() + ".";
        var next = map
            .Where(v => !v.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(v => v.Key, v => v.Value);

        foreach (var (key, value) in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                next[prefix + key] = value.Trim();
        }

        return next;
    }
}
