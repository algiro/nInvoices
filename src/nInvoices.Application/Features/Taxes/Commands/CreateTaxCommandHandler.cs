using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

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

    public CreateTaxCommandHandler(
        IRepository<Tax> taxRepository,
        IRepository<Customer> customerRepository,
        IUnitOfWork unitOfWork)
    {
        _taxRepository = taxRepository;
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TaxDto> Handle(CreateTaxCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Tax;

        var customer = await _customerRepository.GetByIdAsync(dto.CustomerId, cancellationToken);
        if (customer == null)
            throw new KeyNotFoundException($"Customer with ID {dto.CustomerId} not found");

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

        await _taxRepository.AddAsync(tax, cancellationToken);
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