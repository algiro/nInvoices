using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Compliance.Queries;

/// <summary>The country regimes this installation offers, with the current user settings for each.</summary>
public sealed record GetComplianceCountriesQuery : IRequest<IReadOnlyList<ComplianceCountryDto>>;
