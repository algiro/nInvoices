using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Rates.Commands;

/// <summary>
/// Command to update an existing rate.
/// </summary>
public sealed record UpdateRateCommand(long Id, UpdateRateDto Rate) : IRequest<RateDto>;
