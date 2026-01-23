using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceTemplates.Queries;

public sealed class GetTemplateByCustomerAndTypeQueryHandler : IRequestHandler<GetTemplateByCustomerAndTypeQuery, InvoiceTemplateDto?>
{
    private readonly IRepository<InvoiceTemplate> _repository;

    public GetTemplateByCustomerAndTypeQueryHandler(IRepository<InvoiceTemplate> repository)
    {
        _repository = repository;
    }

    public async Task<InvoiceTemplateDto?> Handle(GetTemplateByCustomerAndTypeQuery request, CancellationToken cancellationToken)
    {
        var templates = await _repository.FindAsync(
            t => t.CustomerId == request.CustomerId && t.InvoiceType == request.Type && t.IsActive, 
            cancellationToken);

        var template = templates.FirstOrDefault();
        
        if (template == null)
            return null;

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