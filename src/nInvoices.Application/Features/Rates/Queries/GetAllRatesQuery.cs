using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Rates.Queries;

public sealed record GetAllRatesQuery : IRequest<IEnumerable<RateDto>>;