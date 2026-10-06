using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
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

    public async ValueTask<IEnumerable<InvoiceTemplateDto>> Handle(GetAllInvoiceTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await _repository.GetAllAsync(cancellationToken);

        return templates.Select(InvoiceTemplateMapper.ToDto);
    }
}