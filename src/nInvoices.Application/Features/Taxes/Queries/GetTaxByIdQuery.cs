using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Taxes.Queries;

public sealed record GetTaxByIdQuery(long Id) : IRequest<TaxDto?>;