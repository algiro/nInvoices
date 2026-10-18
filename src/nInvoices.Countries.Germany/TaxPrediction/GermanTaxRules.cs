using System.Globalization;

namespace nInvoices.Countries.Germany.TaxPrediction;

/// <summary>
/// The rates, limits and dates of the German taxes of a self-employed person. When the law changes, the value
/// depends on the year here and nowhere else. Years before the first known one use it; years after the last
/// use the last (the estimate says so).
/// </summary>
public static class GermanTaxRules
{
    /// <summary>The general VAT rate, used to self-charge a reverse-charge expense (§ 13b UStG).</summary>
    public const decimal VatGeneralRate = 19m;

    public const decimal SolidarityRate = 0.055m;

    /// <summary>Above the exemption limit the Soli is at most this share of what exceeds it (Milderungszone).</summary>
    public const decimal SolidarityPhaseIn = 0.119m;

    /// <summary>Sonderausgaben-Pauschbetrag (§ 10c EStG), single assessment.</summary>
    public const decimal SpecialExpensesAllowance = 36m;

    /// <summary>Gewerbesteuer: the allowance of a natural person (§ 11 (1) GewStG).</summary>
    public const decimal TradeTaxAllowance = 24_500m;

    /// <summary>Gewerbesteuer: the Steuermesszahl (§ 11 (2) GewStG).</summary>
    public const decimal TradeTaxBaseRate = 0.035m;

    /// <summary>The Gewerbesteuer credited against the Einkommensteuer, as a multiple of the Messbetrag (§ 35 EStG).</summary>
    public const decimal TradeTaxCreditFactor = 4.0m;

    /// <summary>Kleinunternehmer (§ 19 UStG, from 2025): revenue of the previous year at most this...</summary>
    public const decimal SmallBusinessPreviousYearLimit = 25_000m;

    /// <summary>...and of the current year at most this; above it VAT is due from the invoice that crosses it.</summary>
    public const decimal SmallBusinessCurrentYearLimit = 100_000m;

    public const int FirstKnownYear = 2024;
    public const int LastKnownYear = 2026;

    private sealed record Tariff(decimal BasicAllowance, decimal Zone2End, decimal A2, decimal Zone3End, decimal A3, decimal C3, decimal C4, decimal C5);

    // § 32a EStG: Grundfreibetrag, the two progressive zones (their end and coefficients), the 42% and 45% zones
    private static Tariff TariffOf(int year) => Math.Clamp(year, FirstKnownYear, LastKnownYear) switch
    {
        2024 => new Tariff(11_784m, 17_005m, 954.80m, 66_760m, 181.19m, 991.21m, 10_636.31m, 18_971.06m),
        2025 => new Tariff(12_096m, 17_443m, 932.30m, 68_480m, 176.64m, 1_015.13m, 10_911.92m, 19_246.67m),
        _ => new Tariff(12_348m, 17_799m, 914.51m, 69_878m, 173.10m, 1_034.87m, 11_135.63m, 19_470.38m)
    };

    /// <summary>The Freigrenze of the Solidaritätszuschlag (single assessment): no Soli on an Einkommensteuer up to it.</summary>
    public static decimal SolidarityExemption(int year) => Math.Clamp(year, FirstKnownYear, LastKnownYear) switch
    {
        2024 => 18_130m,
        2025 => 19_950m,
        _ => 20_350m
    };

    /// <summary>Einkommensteuer of a single assessment (Grundtarif, § 32a EStG), in whole euros.</summary>
    public static decimal IncomeTax(decimal taxableIncome, int year)
    {
        var t = TariffOf(year);
        var x = Math.Floor(Math.Max(0m, taxableIncome));

        decimal tax;
        if (x <= t.BasicAllowance)
        {
            tax = 0m;
        }
        else if (x <= t.Zone2End)
        {
            var y = (x - t.BasicAllowance) / 10_000m;
            tax = (t.A2 * y + 1_400m) * y;
        }
        else if (x <= t.Zone3End)
        {
            var z = (x - t.Zone2End) / 10_000m;
            tax = (t.A3 * z + 2_397m) * z + t.C3;
        }
        else if (x <= 277_825m)
        {
            tax = 0.42m * x - t.C4;
        }
        else
        {
            tax = 0.45m * x - t.C5;
        }

        return Math.Floor(tax);
    }

    /// <summary>The Solidaritätszuschlag on an Einkommensteuer: none up to the Freigrenze, then phased in.</summary>
    public static decimal SolidaritySurcharge(decimal incomeTax, int year)
    {
        var exemption = SolidarityExemption(year);
        if (incomeTax <= exemption)
            return 0m;
        return Round(Math.Min(incomeTax * SolidarityRate, (incomeTax - exemption) * SolidarityPhaseIn));
    }

    /// <summary>The Steuermessbetrag of a profit: rounded down to €100, less the allowance, times 3.5%.</summary>
    public static decimal TradeTaxBase(decimal profit)
    {
        var income = Math.Floor(Math.Max(0m, profit) / 100m) * 100m;
        return Round(Math.Max(0m, income - TradeTaxAllowance) * TradeTaxBaseRate);
    }

    /// <summary>The 10th of the month after the period, one month later with the Dauerfristverlängerung.</summary>
    public static DateOnly VatDeadline(DateOnly periodEnd, bool extension) =>
        NextWorkingDay(new DateOnly(periodEnd.Year, periodEnd.Month, 1).AddMonths(extension ? 2 : 1).AddDays(9));

    /// <summary>Einkommensteuer-Vorauszahlungen: 10 March, June, September and December (§ 37 EStG).</summary>
    public static DateOnly IncomeTaxPrepaymentDeadline(int year, int quarter) =>
        NextWorkingDay(new DateOnly(year, quarter * 3, 10));

    /// <summary>Gewerbesteuer-Vorauszahlungen: 15 February, May, August and November (§ 19 GewStG).</summary>
    public static DateOnly TradeTaxPrepaymentDeadline(int year, int quarter) =>
        NextWorkingDay(new DateOnly(year, quarter * 3 - 1, 15));

    /// <summary>The annual returns of a year, filed without an adviser: 31 July of the next year.</summary>
    public static DateOnly AnnualReturnDeadline(int year) => NextWorkingDay(new DateOnly(year + 1, 7, 31));

    /// <summary>A deadline on a weekend moves to the Monday (public holidays aside).</summary>
    public static DateOnly NextWorkingDay(DateOnly day) => day.DayOfWeek switch
    {
        DayOfWeek.Saturday => day.AddDays(2),
        DayOfWeek.Sunday => day.AddDays(1),
        _ => day
    };

    /// <summary>A number the user typed in the settings, "1234.5" or "1234,5"; null when empty or not a number.</summary>
    public static decimal? ParseAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
