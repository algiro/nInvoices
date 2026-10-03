namespace nInvoices.Application.DTOs;

/// <summary>
/// One country regime as offered to the current user: what the country involves, the extra
/// fields it asks for, and the user's own settings for it.
/// </summary>
public sealed record ComplianceCountryDto(
    string CountryCode,
    string Name,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<ComplianceFieldDto> Fields,
    IReadOnlyList<ComplianceFieldDto> CustomerFields,
    bool RequiresCertificate,
    ComplianceSettingsDto Settings);

public sealed record ComplianceFieldDto(
    string Key,
    string Label,
    string Type,
    bool Required,
    string? Help,
    IReadOnlyList<ComplianceFieldOptionDto>? Options);

public sealed record ComplianceFieldOptionDto(string Value, string Label);

public sealed record ComplianceSettingsDto(
    bool IsEnabled,
    string? LegalName,
    string? TaxId,
    AddressDto? Address,
    IReadOnlyDictionary<string, string> Values,
    SigningCertificateDto? Certificate = null);

/// <summary>What is shown about the uploaded signing certificate; the certificate itself is never returned.</summary>
public sealed record SigningCertificateDto(string Subject, string Thumbprint, DateTime NotAfter, bool IsExpired);

/// <summary>
/// Replaces the user's settings for a country. Turning the regime on checks them against the
/// country rules; while it is off they are saved as they are.
/// </summary>
public sealed record UpdateComplianceSettingsDto(
    bool IsEnabled,
    string? LegalName,
    string? TaxId,
    AddressDto? Address,
    Dictionary<string, string>? Values);
