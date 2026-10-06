using Mediator;
using nInvoices.Application.Compliance;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Application.Exceptions;

namespace nInvoices.Application.Features.Taxes.Commands;

/// <summary>
/// Handles tax configuration creation.
/// Validates customer exists and tax handler is registered.
/// </summary>
public sealed class CreateTaxCommandHandler : IRequestHandler<CreateTaxCommand, TaxDto>
{
    private readonly IRepository<Tax> _taxRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITaxCompliance _compliance;

    public CreateTaxCommandHandler(
        IRepository<Tax> taxRepository,
        IRepository<Customer> customerRepository,
        IUnitOfWork unitOfWork,
        ITaxCompliance compliance)
    {
        _compliance = compliance;
        _taxRepository = taxRepository;
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<TaxDto> Handle(CreateTaxCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Tax;

        var customer = await _customerRepository.GetByIdAsync(dto.CustomerId, cancellationToken);
        if (customer == null)
            throw new NotFoundException($"Customer with ID {dto.CustomerId} not found");

        var taxId = string.IsNullOrWhiteSpace(dto.TaxId)
            ? dto.Description.ToUpperInvariant().Replace(" ", "_")
            : dto.TaxId;

        var tax = new Tax(
            dto.CustomerId,
            taxId,
            dto.Description,
            dto.HandlerId,
            dto.Rate,
            dto.ApplicationType,
            dto.Order)
        {
            AppliedToTaxId = dto.AppliedToTaxId
        };

        await _compliance.ApplyAsync(tax, dto.ComplianceValues, cancellationToken);

        await _taxRepository.AddAsync(tax, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(tax);
    }

    private static TaxDto MapToDto(Tax tax) => TaxMapper.ToDto(tax);
}