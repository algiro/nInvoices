using MediatR;
using nInvoices.Application.DTOs;
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

    public async Task<IEnumerable<TaxDto>> Handle(GetAllTaxesQuery request, CancellationToken cancellationToken)
    {
        var taxes = await _repository.GetAllAsync(cancellationToken);

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