using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceTemplates.Queries;

public sealed class GetTemplatesByCustomerIdQueryHandler : IRequestHandler<GetTemplatesByCustomerIdQuery, IEnumerable<InvoiceTemplateDto>>
{
    private readonly IRepository<InvoiceTemplate> _repository;

    public GetTemplatesByCustomerIdQueryHandler(IRepository<InvoiceTemplate> repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<InvoiceTemplateDto>> Handle(GetTemplatesByCustomerIdQuery request, CancellationToken cancellationToken)
    {
        var templates = await _repository.FindAsync(
            t => t.CustomerId == request.CustomerId, 
            cancellationToken);

        return templates.Select(InvoiceTemplateMapper.ToDto);
    }
}