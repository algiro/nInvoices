using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Rates.Commands;

/// <summary>
/// Command to create a new rate for a customer.
/// Encapsulates the intention to add pricing information.
/// </summary>
public sealed record CreateRateCommand(CreateRateDto Rate) : IRequest<RateDto>;
