using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Handles invoice template creation. Validates the customer exists; the syntax is checked before
/// this runs (<c>CreateInvoiceTemplateCommandValidator</c>).
/// </summary>
public sealed class CreateInvoiceTemplateCommandHandler : IRequestHandler<CreateInvoiceTemplateCommand, InvoiceTemplateDto>
{
    private readonly IRepository<InvoiceTemplate> _templateRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateInvoiceTemplateCommandHandler(
        IRepository<InvoiceTemplate> templateRepository,
        IRepository<Customer> customerRepository,
        IUnitOfWork unitOfWork)
    {
        _templateRepository = templateRepository;
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<InvoiceTemplateDto> Handle(CreateInvoiceTemplateCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Template;

        if (dto.CustomerId.HasValue)
        {
            var customer = await _customerRepository.GetByIdAsync(dto.CustomerId.Value, cancellationToken);
            if (customer == null)
                throw new KeyNotFoundException($"Customer with ID {dto.CustomerId} not found");
        }

        var template = new InvoiceTemplate(dto.CustomerId, dto.InvoiceType, dto.Name, dto.Content);

        await _templateRepository.AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(template);
    }

    private static InvoiceTemplateDto MapToDto(InvoiceTemplate template) => new(
        template.Id,
        template.CustomerId,
        template.InvoiceType,
        template.Name,
        template.Content,
        template.IsActive,
        template.CreatedAt,
        template.UpdatedAt ?? template.CreatedAt);
}