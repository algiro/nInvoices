namespace nInvoices.Application.DTOs;

/// <summary>A country this installation supports, and what it has for it.</summary>
/// <param name="HasInvoicingRules">It has invoicing rules (fields, checks, e-invoices).</param>
/// <param name="HasTaxPrediction">Its taxes can be estimated.</param>
public sealed record OfferedCountryDto(string CountryCode, string Name, bool HasInvoicingRules, bool HasTaxPrediction);

/// <summary>The country the user works from, and the ones they can choose.</summary>
/// <param name="CountryCode">Null until chosen or inferred from what the user already uses.</param>
/// <param name="IsChosen">False while it is only inferred: the user hasn't confirmed it.</param>
public sealed record UserCountryDto(string? CountryCode, bool IsChosen, IReadOnlyList<OfferedCountryDto> Offered);

public sealed record SetUserCountryDto(string CountryCode);
