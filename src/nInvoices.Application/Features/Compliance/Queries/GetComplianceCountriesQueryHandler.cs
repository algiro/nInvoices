using MediatR;
using nInvoices.Application.Compliance;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Compliance.Queries;

public sealed class GetComplianceCountriesQueryHandler : IRequestHandler<GetComplianceCountriesQuery, IReadOnlyList<ComplianceCountryDto>>
{
    private readonly IComplianceRegistry _registry;
    private readonly IRepository<ComplianceSettings> _settings;

    public GetComplianceCountriesQueryHandler(IComplianceRegistry registry, IRepository<ComplianceSettings> settings)
    {
        _registry = registry;
        _settings = settings;
    }

    public async Task<IReadOnlyList<ComplianceCountryDto>> Handle(GetComplianceCountriesQuery request, CancellationToken cancellationToken)
    {
        if (_registry.Offered.Count == 0)
            return [];

        // The query filter returns only the current user's rows
        var saved = (await _settings.GetAllAsync(cancellationToken))
            .ToDictionary(s => s.CountryCode, StringComparer.OrdinalIgnoreCase);

        return _registry.Offered
            .Select(m => ComplianceMapper.ToDto(m, saved.GetValueOrDefault(m.CountryCode), _registry.RequiresSigningCertificate(m.CountryCode)))
            .ToList();
    }
}
