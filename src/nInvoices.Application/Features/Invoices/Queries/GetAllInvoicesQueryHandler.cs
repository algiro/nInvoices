using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Queries;

public sealed class GetAllInvoicesQueryHandler : IRequestHandler<GetAllInvoicesQuery, IEnumerable<InvoiceDto>>
{
    private readonly IRepository<Invoice> _repository;

    public GetAllInvoicesQueryHandler(IRepository<Invoice> repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<InvoiceDto>> Handle(GetAllInvoicesQuery request, CancellationToken cancellationToken)
    {
        var invoices = await _repository.GetAllAsync(cancellationToken);

        return invoices.Select(InvoiceMapper.ToDto);
    }
}
