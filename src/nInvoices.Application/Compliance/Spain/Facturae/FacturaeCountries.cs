using System.Globalization;

namespace nInvoices.Application.Compliance.Spain.Facturae;

/// <summary>Country facts Facturae needs: three-letter codes and the residence type of a party.</summary>
internal static class FacturaeCountries
{
    private static readonly HashSet<string> EuMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR", "HU", "IE",
        "IT", "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SK", "SI", "ES", "SE"
    };

    public const string Spain = "ES";

    /// <summary>R: resident in Spain, U: in the European Union, E: elsewhere.</summary>
    public static string ResidenceTypeCode(string? alpha2) =>
        string.Equals(alpha2, Spain, StringComparison.OrdinalIgnoreCase) ? "R"
        : alpha2 is not null && EuMembers.Contains(alpha2) ? "U"
        : "E";

    /// <summary>The ISO 3166-1 alpha-3 code Facturae uses ("ESP"), or null for an unknown country.</summary>
    public static string? Alpha3(string? alpha2)
    {
        if (string.IsNullOrWhiteSpace(alpha2))
            return null;

        try
        {
            return new RegionInfo(alpha2.Trim()).ThreeLetterISORegionName.ToUpperInvariant();
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
