using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Taxes.Queries;

public sealed class GetAllTaxesQueryHandler : IRequestHandler<GetAllTaxesQuery, IEnumerable<TaxDto>>
{
    private readonly IRepository<Tax> _repository;

    public GetAllTaxesQueryHandler(IRepository<Tax> repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<TaxDto>> Handle(GetAllTaxesQuery request, CancellationToken cancellationToken)
    {
        var taxes = await _repository.GetAllAsync(cancellationToken);

        return taxes.Select(TaxMapper.ToDto);
    }
}