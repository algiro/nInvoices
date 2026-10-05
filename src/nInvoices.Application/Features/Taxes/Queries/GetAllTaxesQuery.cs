using Mediator;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Taxes.Queries;

public sealed record GetAllTaxesQuery : IRequest<IEnumerable<TaxDto>>;