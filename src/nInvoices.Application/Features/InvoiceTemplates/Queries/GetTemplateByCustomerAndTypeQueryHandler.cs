using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
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

    public async ValueTask<InvoiceTemplateDto?> Handle(GetTemplateByCustomerAndTypeQuery request, CancellationToken cancellationToken)
    {
        // The customer's own active template, else the shared one
        var templates = await _repository.FindAsync(
            t => (t.CustomerId == request.CustomerId || t.CustomerId == null) && t.InvoiceType == request.Type && t.IsActive,
            cancellationToken);

        var template = ScopedTemplates.PickEffective(templates, request.CustomerId);
        
        if (template == null)
            return null;

        return InvoiceTemplateMapper.ToDto(template);
    }
}