using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Queries;

public sealed class GetInvoicesByCustomerQueryHandler : IRequestHandler<GetInvoicesByCustomerQuery, IEnumerable<InvoiceDto>>
{
    private readonly IRepository<Invoice> _repository;

    public GetInvoicesByCustomerQueryHandler(IRepository<Invoice> repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<InvoiceDto>> Handle(GetInvoicesByCustomerQuery request, CancellationToken cancellationToken)
    {
        var invoices = await _repository.FindAsync(
            i => i.CustomerId == request.CustomerId,
            cancellationToken);

        return invoices.Select(InvoiceMapper.ToDto);
    }
}
