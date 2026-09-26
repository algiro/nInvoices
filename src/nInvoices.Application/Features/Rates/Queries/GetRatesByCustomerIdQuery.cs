using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Rates.Queries;

/// <summary>
/// Query to get all rates for a specific customer.
/// Essential for invoice generation and rate selection.
/// </summary>
public sealed record GetRatesByCustomerIdQuery(long CustomerId) : IRequest<IEnumerable<RateDto>>;