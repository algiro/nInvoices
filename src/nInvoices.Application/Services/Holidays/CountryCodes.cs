using System.Globalization;

namespace nInvoices.Application.Services.Holidays;

/// <summary>
/// Turns the free-text country of an address ("Italy", "Italia", "IT", "ITA") into its
/// ISO 3166-1 alpha-2 code, using the names .NET knows for each region.
/// </summary>
public static class CountryCodes
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> ByName = new(BuildIndex);

    /// <summary>The two-letter code for <paramref name="country"/>, or null when it isn't recognized.</summary>
    public static string? FromName(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
            return null;

        return ByName.Value.TryGetValue(country.Trim(), out var code) ? code : null;
    }

    /// <summary>The country's English name for a two-letter code, or the code itself when unknown.</summary>
    public static string DisplayName(string countryCode)
    {
        try
        {
            return new RegionInfo(countryCode).EnglishName;
        }
        catch (ArgumentException)
        {
            return countryCode;
        }
    }

    private static IReadOnlyDictionary<string, string> BuildIndex()
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            RegionInfo region;
            try
            {
                region = new RegionInfo(culture.Name);
            }
            catch (ArgumentException)
            {
                continue;
            }

            var code = region.TwoLetterISORegionName;
            if (code.Length != 2 || !code.All(char.IsAsciiLetter))
                continue;

            foreach (var name in new[] { code, region.ThreeLetterISORegionName, region.EnglishName, region.NativeName })
                index.TryAdd(name, code.ToUpperInvariant());
        }

        // Common names .NET spells differently
        index.TryAdd("UK", "GB");
        index.TryAdd("Great Britain", "GB");
        index.TryAdd("England", "GB");
        index.TryAdd("USA", "US");
        index.TryAdd("United States of America", "US");

        return index;
    }
}
