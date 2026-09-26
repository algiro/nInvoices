using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Rates.Queries;

public sealed class GetRateByIdQueryHandler : IRequestHandler<GetRateByIdQuery, RateDto?>
{
    private readonly IRepository<Rate> _repository;

    public GetRateByIdQueryHandler(IRepository<Rate> repository)
    {
        _repository = repository;
    }

    public async Task<RateDto?> Handle(GetRateByIdQuery request, CancellationToken cancellationToken)
    {
        var rate = await _repository.GetByIdAsync(request.Id, cancellationToken);
        
        if (rate == null)
            return null;

        return new RateDto(
            rate.Id,
            rate.CustomerId,
            rate.Type,
            new MoneyDto(rate.Price.Amount, rate.Price.Currency),
            rate.CreatedAt,
            rate.UpdatedAt ?? rate.CreatedAt);
    }
}