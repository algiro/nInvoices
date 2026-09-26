using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Rates.Queries;

public sealed class GetRatesByCustomerIdQueryHandler : IRequestHandler<GetRatesByCustomerIdQuery, IEnumerable<RateDto>>
{
    private readonly IRepository<Rate> _repository;

    public GetRatesByCustomerIdQueryHandler(IRepository<Rate> repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<RateDto>> Handle(GetRatesByCustomerIdQuery request, CancellationToken cancellationToken)
    {
        var rates = await _repository.FindAsync(
            r => r.CustomerId == request.CustomerId, 
            cancellationToken);

        return rates.Select(rate => new RateDto(
            rate.Id,
            rate.CustomerId,
            rate.Type,
            new MoneyDto(rate.Price.Amount, rate.Price.Currency),
            rate.CreatedAt,
            rate.UpdatedAt ?? rate.CreatedAt));
    }
}