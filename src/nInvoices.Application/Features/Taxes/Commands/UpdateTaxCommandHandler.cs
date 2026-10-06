using Mediator;
using nInvoices.Application.Compliance;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Application.Exceptions;

namespace nInvoices.Application.Features.Taxes.Commands;

public sealed class UpdateTaxCommandHandler : IRequestHandler<UpdateTaxCommand, TaxDto>
{
    private readonly IRepository<Tax> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITaxCompliance _compliance;

    public UpdateTaxCommandHandler(IRepository<Tax> repository, IUnitOfWork unitOfWork, ITaxCompliance compliance)
    {
        _compliance = compliance;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<TaxDto> Handle(UpdateTaxCommand request, CancellationToken cancellationToken)
    {
        var tax = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (tax == null)
            throw new NotFoundException($"Tax with ID {request.Id} not found");

        var dto = request.Tax;

        tax.Description = dto.Description;
        tax.HandlerId = dto.HandlerId;
        tax.Rate = dto.Rate;
        tax.ApplicationType = dto.ApplicationType;
        tax.AppliedToTaxId = dto.AppliedToTaxId;
        tax.Order = dto.Order;
        tax.IsActive = dto.IsActive;

        await _compliance.ApplyAsync(tax, dto.ComplianceValues, cancellationToken);

        await _repository.UpdateAsync(tax, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return TaxMapper.ToDto(tax);
    }
}