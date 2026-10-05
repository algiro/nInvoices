using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Rates.Queries;

public sealed class GetAllRatesQueryHandler : IRequestHandler<GetAllRatesQuery, IEnumerable<RateDto>>
{
    private readonly IRepository<Rate> _repository;

    public GetAllRatesQueryHandler(IRepository<Rate> repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<RateDto>> Handle(GetAllRatesQuery request, CancellationToken cancellationToken)
    {
        var rates = await _repository.GetAllAsync(cancellationToken);

        return rates.Select(RateMapper.ToDto);
    }
}