using nInvoices.Application.DTOs;
using nInvoices.Core.Compliance;
using nInvoices.Core.Entities;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Mappings;

public static class ComplianceMapper
{
    /// <param name="settings">The user's settings for the country; null if none were saved yet.</param>
    public static ComplianceCountryDto ToDto(ICountryComplianceModule module, ComplianceSettings? settings, bool requiresCertificate) => new(
        module.CountryCode,
        module.DisplayName,
        module.Capabilities.Select(c => c.ToString()).Order().ToList(),
        module.Fields.Select(ToDto).ToList(),
        module.CustomerFields.Select(ToDto).ToList(),
        requiresCertificate,
        settings is null
            ? new ComplianceSettingsDto(false, null, null, null, new Dictionary<string, string>())
            : new ComplianceSettingsDto(
                settings.IsEnabled,
                settings.LegalName,
                settings.TaxId,
                ToDto(settings.Address),
                settings.Values,
                settings.HasCertificate
                    ? new SigningCertificateDto(
                        settings.CertificateSubject ?? "",
                        settings.CertificateThumbprint ?? "",
                        settings.CertificateNotAfter ?? DateTime.MinValue,
                        settings.CertificateNotAfter < DateTime.UtcNow)
                    : null));

    private static ComplianceFieldDto ToDto(ComplianceField f) => new(
        f.Key,
        f.Label,
        f.Type.ToString(),
        f.Required,
        f.Help,
        f.Options?.Select(o => new ComplianceFieldOptionDto(o.Value, o.Label)).ToList());

    /// <returns>The address, or null when every part is blank.</returns>
    /// <exception cref="ArgumentException">Only some parts are filled in.</exception>
    public static Address? ToAddress(AddressDto? dto)
    {
        if (dto is null)
            return null;

        var parts = new[] { dto.Street, dto.HouseNumber, dto.City, dto.ZipCode, dto.Country, dto.State };
        if (parts.All(string.IsNullOrWhiteSpace))
            return null;

        return new Address(
            dto.Street?.Trim() ?? "",
            dto.HouseNumber?.Trim() ?? "",
            dto.City?.Trim() ?? "",
            dto.ZipCode?.Trim() ?? "",
            dto.Country?.Trim() ?? "",
            string.IsNullOrWhiteSpace(dto.State) ? null : dto.State.Trim());
    }

    private static AddressDto? ToDto(Address? address) => address is null
        ? null
        : new AddressDto(address.Street, address.HouseNumber, address.City, address.ZipCode, address.Country, address.State);
}
