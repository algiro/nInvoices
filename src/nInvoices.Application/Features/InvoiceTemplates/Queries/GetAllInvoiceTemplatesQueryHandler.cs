using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceTemplates.Queries;

public sealed class GetAllInvoiceTemplatesQueryHandler : IRequestHandler<GetAllInvoiceTemplatesQuery, IEnumerable<InvoiceTemplateDto>>
{
    private readonly IRepository<InvoiceTemplate> _repository;

    public GetAllInvoiceTemplatesQueryHandler(IRepository<InvoiceTemplate> repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<InvoiceTemplateDto>> Handle(GetAllInvoiceTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await _repository.GetAllAsync(cancellationToken);

        return templates.Select(MapToDto);
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