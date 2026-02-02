using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceTemplates.Queries;

public sealed class GetInvoiceTemplateByIdQueryHandler : IRequestHandler<GetInvoiceTemplateByIdQuery, InvoiceTemplateDto?>
{
    private readonly IRepository<InvoiceTemplate> _repository;

    public GetInvoiceTemplateByIdQueryHandler(IRepository<InvoiceTemplate> repository)
    {
        _repository = repository;
    }

    public async Task<InvoiceTemplateDto?> Handle(GetInvoiceTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        
        if (template == null)
            return null;

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