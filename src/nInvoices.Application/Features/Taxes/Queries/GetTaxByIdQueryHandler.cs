using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Taxes.Queries;

public sealed class GetTaxByIdQueryHandler : IRequestHandler<GetTaxByIdQuery, TaxDto?>
{
    private readonly IRepository<Tax> _repository;

    public GetTaxByIdQueryHandler(IRepository<Tax> repository)
    {
        _repository = repository;
    }

    public async Task<TaxDto?> Handle(GetTaxByIdQuery request, CancellationToken cancellationToken)
    {
        var tax = await _repository.GetByIdAsync(request.Id, cancellationToken);
        
        if (tax == null)
            return null;

        return MapToDto(tax);
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