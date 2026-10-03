using MediatR;
using nInvoices.Application.Compliance;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Compliance;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Compliance.Commands;

public sealed class UpdateComplianceSettingsCommandHandler : IRequestHandler<UpdateComplianceSettingsCommand, ComplianceCountryDto?>
{
    private readonly IComplianceRegistry _registry;
    private readonly IRepository<ComplianceSettings> _settings;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateComplianceSettingsCommandHandler(
        IComplianceRegistry registry,
        IRepository<ComplianceSettings> settings,
        IUnitOfWork unitOfWork)
    {
        _registry = registry;
        _settings = settings;
        _unitOfWork = unitOfWork;
    }

    /// <exception cref="ComplianceValidationException">The regime is being turned on and the settings break its rules.</exception>
    /// <exception cref="ArgumentException">The address is incomplete.</exception>
    public async Task<ComplianceCountryDto?> Handle(UpdateComplianceSettingsCommand request, CancellationToken cancellationToken)
    {
        var module = _registry.Find(request.CountryCode);
        if (module is null)
            return null;

        var dto = request.Settings;
        var address = ComplianceMapper.ToAddress(dto.Address);

        // Keep only the values the module asks for
        var known = module.Fields.Select(f => f.Key).ToHashSet();
        var values = (dto.Values ?? new Dictionary<string, string>())
            .Where(v => known.Contains(v.Key))
            .ToDictionary(v => v.Key, v => v.Value);

        if (dto.IsEnabled)
        {
            var issues = module.ValidateIssuer(new IssuerProfile(dto.LegalName, dto.TaxId, address, values));
            if (issues.Count > 0)
                throw new ComplianceValidationException(issues);
        }

        // The query filter returns only the current user's rows
        var settings = (await _settings.GetAllAsync(cancellationToken))
            .FirstOrDefault(s => string.Equals(s.CountryCode, module.CountryCode, StringComparison.OrdinalIgnoreCase));

        if (settings is null)
        {
            settings = new ComplianceSettings(module.CountryCode);
            settings.Update(dto.IsEnabled, dto.LegalName, dto.TaxId, address, values);
            await _settings.AddAsync(settings, cancellationToken);
        }
        else
        {
            settings.Update(dto.IsEnabled, dto.LegalName, dto.TaxId, address, values);
            await _settings.UpdateAsync(settings, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ComplianceMapper.ToDto(module, settings);
    }
}
