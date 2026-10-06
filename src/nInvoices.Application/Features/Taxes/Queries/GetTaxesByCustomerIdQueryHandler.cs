using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Taxes.Queries;

public sealed class GetTaxesByCustomerIdQueryHandler : IRequestHandler<GetTaxesByCustomerIdQuery, IEnumerable<TaxDto>>
{
    private readonly IRepository<Tax> _repository;

    public GetTaxesByCustomerIdQueryHandler(IRepository<Tax> repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<TaxDto>> Handle(GetTaxesByCustomerIdQuery request, CancellationToken cancellationToken)
    {
        var taxes = await _repository.FindAsync(
            t => t.CustomerId == request.CustomerId, 
            cancellationToken);

        return taxes.Select(TaxMapper.ToDto);
    }
}