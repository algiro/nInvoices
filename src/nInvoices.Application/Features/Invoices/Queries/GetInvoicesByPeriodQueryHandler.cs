using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Queries;

public sealed class GetInvoicesByPeriodQueryHandler : IRequestHandler<GetInvoicesByPeriodQuery, IEnumerable<InvoiceDto>>
{
    private readonly IRepository<Invoice> _repository;

    public GetInvoicesByPeriodQueryHandler(IRepository<Invoice> repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<InvoiceDto>> Handle(GetInvoicesByPeriodQuery request, CancellationToken cancellationToken)
    {
        var invoices = await _repository.FindAsync(
            i => i.CustomerId == request.CustomerId &&
                 i.Year == request.Year &&
                 (!request.Month.HasValue || i.Month == request.Month),
            cancellationToken);

        return invoices.Select(InvoiceMapper.ToDto);
    }
}
