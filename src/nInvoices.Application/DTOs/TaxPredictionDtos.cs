namespace nInvoices.Application.DTOs;

/// <summary>
/// The estimate of a year's taxes. When the user hasn't said where they pay taxes yet,
/// <see cref="IsSetUp"/> is false and there are no periods.
/// </summary>
/// <param name="Currency">What every amount is in.</param>
/// <param name="Warnings">What the estimate leaves out or could not work out.</param>
public sealed record TaxPredictionDto(
    int Year,
    bool IsSetUp,
    string? CountryCode,
    string? Currency,
    IReadOnlyList<TaxPeriodDto> Periods,
    IReadOnlyList<string> Warnings);

/// <param name="Period">1-based period of the year (the quarter, for quarterly forms).</param>
/// <param name="Name">E.g. "Q1".</param>
public sealed record TaxPeriodDto(int Period, string Name, DateOnly Start, DateOnly End, IReadOnlyList<TaxFormDto> Forms);

/// <summary>One form to file for a period: the estimate, how it was worked out, and what was paid if recorded.</summary>
/// <param name="Code">Stable code of the form, e.g. "130"; payments are recorded under it.</param>
public sealed record TaxFormDto(
    string Code,
    string Name,
    DateOnly Deadline,
    decimal Estimated,
    TaxPaymentDto? Payment,
    IReadOnlyList<TaxFormLineDto> Lines);

/// <param name="Box">The box of the official form, or null for a line that only explains another.</param>
public sealed record TaxFormLineDto(string? Box, string Label, decimal Amount);

public sealed record TaxPaymentDto(long Id, int Year, int Period, string Form, decimal Amount, string Currency, DateOnly PaidOn);

/// <summary>Records what was actually paid for a form and period, replacing an earlier record of the same.</summary>
public sealed record RecordTaxPaymentDto(int Year, int Period, string Form, decimal Amount, DateOnly PaidOn);

/// <summary>The countries whose taxes can be estimated, and the user's choice among them (null if none yet).</summary>
public sealed record TaxPredictionSettingsDto(IReadOnlyList<TaxCountryDto> Countries, TaxProfileDto? Profile);

/// <param name="IndirectTaxName">What the country's VAT is called, for the expenses ("IVA / IGIC").</param>
public sealed record TaxCountryDto(string CountryCode, string Name, string Currency, string IndirectTaxName, IReadOnlyList<TaxRegimeDto> Regimes);

/// <param name="Defaults">The values a new profile starts with: the form shows them until the user saves.</param>
public sealed record TaxRegimeDto(
    string Id,
    string Name,
    string? Help,
    IReadOnlyList<ComplianceFieldDto> Fields,
    IReadOnlyDictionary<string, string> Defaults);

public sealed record TaxProfileDto(string CountryCode, string RegimeId, IReadOnlyDictionary<string, string> Values);

/// <summary>Where the user pays taxes and how. Values the regime doesn't ask for are dropped.</summary>
public sealed record UpdateTaxProfileDto(string CountryCode, string RegimeId, Dictionary<string, string>? Values);
