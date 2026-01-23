using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Taxes.Queries;

/// <summary>
/// Query to get all tax configurations for a specific customer.
/// Essential for invoice tax calculation.
/// </summary>
public sealed record GetTaxesByCustomerIdQuery(long CustomerId) : IRequest<IEnumerable<TaxDto>>;