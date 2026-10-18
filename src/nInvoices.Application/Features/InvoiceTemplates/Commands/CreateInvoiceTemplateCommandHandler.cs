using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Localization;
using nInvoices.Application.Mappings;
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
    private readonly IAppText _text;

    public CreateInvoiceTemplateCommandHandler(
        IRepository<InvoiceTemplate> templateRepository,
        IRepository<Customer> customerRepository,
        IUnitOfWork unitOfWork,
        IAppText text)
    {
        _templateRepository = templateRepository;
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
        _text = text;
    }

    public async ValueTask<InvoiceTemplateDto> Handle(CreateInvoiceTemplateCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Template;

        if (dto.CustomerId.HasValue)
        {
            var customer = await _customerRepository.GetByIdAsync(dto.CustomerId.Value, cancellationToken);
            if (customer == null)
                throw new NotFoundException(_text.Translate("notFound.customer", ("id", dto.CustomerId)));
        }

        var template = new InvoiceTemplate(dto.CustomerId, dto.InvoiceType, dto.Name, dto.Content);

        await _templateRepository.AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return InvoiceTemplateMapper.ToDto(template);
    }
}