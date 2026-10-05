using Mediator;
using nInvoices.Application.DTOs;
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

    public UpdateInvoiceTemplateCommandHandler(
        IRepository<InvoiceTemplate> repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<InvoiceTemplateDto> Handle(UpdateInvoiceTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template == null)
            throw new KeyNotFoundException($"Template with ID {request.Id} not found");

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