namespace nInvoices.Core.Entities;

/// <summary>
/// The public holidays of one country, as editable rules. Created from the built-in rules the
/// first time the country is used; from then on the stored rules are the ones that apply.
/// </summary>
public sealed class HolidayCalendar : OwnedEntityBase
{
    /// <summary>ISO 3166-1 alpha-2 code, upper case (e.g. "IT").</summary>
    public string CountryCode { get; set; } = string.Empty;

    public ICollection<HolidayRule> Rules { get; set; } = [];

    public HolidayCalendar()
    {
        CreatedAt = DateTime.UtcNow;
    }

    public HolidayCalendar(string countryCode) : this()
    {
        CountryCode = NormalizeCountryCode(countryCode);
    }

    /// <summary>The holidays that fall in <paramref name="year"/>, in date order.</summary>
    public IReadOnlyList<(DateOnly Date, string Name)> HolidaysIn(int year) =>
        Rules
            .Where(r => r.IsActive)
            .Select(r => (Date: r.DateIn(year), r.Name))
            .Where(h => h.Date.HasValue)
            .Select(h => (h.Date!.Value, h.Name))
            .OrderBy(h => h.Item1)
            .ToList();

    public static string NormalizeCountryCode(string countryCode)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        var code = countryCode.Trim().ToUpperInvariant();
        if (code.Length != 2 || !code.All(char.IsAsciiLetterUpper))
            throw new ArgumentException("Country code must be two letters (ISO 3166-1 alpha-2)", nameof(countryCode));

        return code;
    }
}
