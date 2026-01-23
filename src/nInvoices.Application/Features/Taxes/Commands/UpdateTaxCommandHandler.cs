using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Taxes.Commands;

public sealed class UpdateTaxCommandHandler : IRequestHandler<UpdateTaxCommand, TaxDto>
{
    private readonly IRepository<Tax> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTaxCommandHandler(IRepository<Tax> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TaxDto> Handle(UpdateTaxCommand request, CancellationToken cancellationToken)
    {
        var tax = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (tax == null)
            throw new KeyNotFoundException($"Tax with ID {request.Id} not found");

        var dto = request.Tax;

        tax.Description = dto.Description;
        tax.HandlerId = dto.HandlerId;
        tax.Rate = dto.Rate;
        tax.ApplicationType = dto.ApplicationType;
        tax.AppliedToTaxId = dto.AppliedToTaxId;
        tax.Order = dto.Order;
        tax.IsActive = dto.IsActive;

        await _repository.UpdateAsync(tax, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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