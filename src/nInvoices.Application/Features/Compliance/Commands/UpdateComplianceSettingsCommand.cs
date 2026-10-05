using Mediator;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Compliance.Commands;

/// <summary>Result: the country with the saved settings, or null if the installation does not offer that country.</summary>
public sealed record UpdateComplianceSettingsCommand(string CountryCode, UpdateComplianceSettingsDto Settings) : IRequest<ComplianceCountryDto?>;
