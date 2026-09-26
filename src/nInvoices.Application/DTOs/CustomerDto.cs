namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for customer information.
/// <see cref="HolidayCountry"/> is the country chosen for public holidays (null to follow the
/// address); <see cref="EffectiveHolidayCountry"/> is the one that applies, null when the
/// address country isn't recognized.
/// </summary>
public sealed record CustomerDto(
    long Id,
    string Name,
    string FiscalId,
    string Locale,
    AddressDto Address,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? Email = null,
    string? CcEmails = null,
    string? HolidayCountry = null,
    string? EffectiveHolidayCountry = null);
