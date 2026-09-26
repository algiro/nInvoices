using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Handles invoice template creation.
/// Validates customer exists and template syntax is correct.
/// </summary>
public sealed class CreateInvoiceTemplateCommandHandler : IRequestHandler<CreateInvoiceTemplateCommand, InvoiceTemplateDto>
{
    private readonly IRepository<InvoiceTemplate> _templateRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly ITemplateEngine _templateEngine;
    private readonly IUnitOfWork _unitOfWork;

    public CreateInvoiceTemplateCommandHandler(
        IRepository<InvoiceTemplate> templateRepository,
        IRepository<Customer> customerRepository,
        ITemplateEngine templateEngine,
        IUnitOfWork unitOfWork)
    {
        _templateRepository = templateRepository;
        _customerRepository = customerRepository;
        _templateEngine = templateEngine;
        _unitOfWork = unitOfWork;
    }

    public async Task<InvoiceTemplateDto> Handle(CreateInvoiceTemplateCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Template;

        var customer = await _customerRepository.GetByIdAsync(dto.CustomerId, cancellationToken);
        if (customer == null)
            throw new KeyNotFoundException($"Customer with ID {dto.CustomerId} not found");

        if (!_templateEngine.ValidateTemplate(dto.Content, out var errors))
        {
            var errorMessage = string.Join("; ", errors);
            throw new ArgumentException($"Invalid template syntax: {errorMessage}");
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