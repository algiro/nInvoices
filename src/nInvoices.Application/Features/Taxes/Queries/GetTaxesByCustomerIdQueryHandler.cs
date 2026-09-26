using MediatR;
using nInvoices.Application.DTOs;
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

    public async Task<IEnumerable<TaxDto>> Handle(GetTaxesByCustomerIdQuery request, CancellationToken cancellationToken)
    {
        var taxes = await _repository.FindAsync(
            t => t.CustomerId == request.CustomerId, 
            cancellationToken);

        return taxes.Select(MapToDto);
    }

    private static TaxDto MapToDto(Tax tax) => new(
        tax.Id,
        tax.CustomerId,
        tax.TaxId,
        tax.Description,
        tax.HandlerId,
        tax.Rate,
        tax.ApplicationType,
        tax.AppliedToTaxId,
        tax.Order,
        tax.IsActive,
        tax.CreatedAt,
        tax.UpdatedAt ?? tax.CreatedAt);
}