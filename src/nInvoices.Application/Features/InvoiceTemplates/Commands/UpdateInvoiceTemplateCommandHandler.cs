using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Localization;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Handles invoice template updates. The syntax is checked before this runs
/// (<c>UpdateInvoiceTemplateCommandValidator</c>).
/// </summary>
public sealed class UpdateInvoiceTemplateCommandHandler : IRequestHandler<UpdateInvoiceTemplateCommand, InvoiceTemplateDto>
{
    private readonly IRepository<InvoiceTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppText _text;

    public UpdateInvoiceTemplateCommandHandler(
        IRepository<InvoiceTemplate> repository,
        IUnitOfWork unitOfWork,
        IAppText text)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _text = text;
    }

    public async ValueTask<InvoiceTemplateDto> Handle(UpdateInvoiceTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template == null)
            throw new NotFoundException(_text.Translate("notFound.template", ("id", request.Id)));

        var dto = request.Template;
        template.UpdateContent(dto.Name, dto.Content);

        if (dto.IsActive && !template.IsActive)
        {
            // Turning a template on replaces the customer's active one of the same type
            await InvoiceTemplateActivation.ActivateAsync(template, _repository, _unitOfWork, cancellationToken);
        }
        else
        {
            if (!dto.IsActive)
                template.Deactivate();
            await _repository.UpdateAsync(template, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return InvoiceTemplateMapper.ToDto(template);
    }
}