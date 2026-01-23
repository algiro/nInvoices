using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Rates.Queries;

public sealed record GetRateByIdQuery(long Id) : IRequest<RateDto?>;