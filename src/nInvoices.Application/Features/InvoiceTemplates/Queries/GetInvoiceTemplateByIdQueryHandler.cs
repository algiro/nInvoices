using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
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

    public async ValueTask<InvoiceTemplateDto?> Handle(GetInvoiceTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        
        if (template == null)
            return null;

        return InvoiceTemplateMapper.ToDto(template);
    }
}