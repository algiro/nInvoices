using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Handles invoice template updates.
/// Validates template syntax before saving.
/// </summary>
public sealed class UpdateInvoiceTemplateCommandHandler : IRequestHandler<UpdateInvoiceTemplateCommand, InvoiceTemplateDto>
{
    private readonly IRepository<InvoiceTemplate> _repository;
    private readonly ITemplateEngine _templateEngine;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateInvoiceTemplateCommandHandler(
        IRepository<InvoiceTemplate> repository,
        ITemplateEngine templateEngine,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _templateEngine = templateEngine;
        _unitOfWork = unitOfWork;
    }

    public async Task<InvoiceTemplateDto> Handle(UpdateInvoiceTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template == null)
            throw new KeyNotFoundException($"Template with ID {request.Id} not found");

        var dto = request.Template;

        if (!_templateEngine.ValidateTemplate(dto.Content, out var errors))
        {
            var errorMessage = string.Join("; ", errors);
            throw new ArgumentException($"Invalid template syntax: {errorMessage}");
        }

        template.Content = dto.Content;
        template.IsActive = dto.IsActive;

        await _repository.UpdateAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(template);
    }

    private static InvoiceTemplateDto MapToDto(InvoiceTemplate template) => new(
        template.Id,
        template.CustomerId,
        template.InvoiceType,
        template.Content,
        template.IsActive,
        template.CreatedAt,
        template.UpdatedAt ?? template.CreatedAt);
}