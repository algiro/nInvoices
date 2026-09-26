using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Invoices.Queries;

public sealed record GetInvoicesByPeriodQuery(
    long CustomerId,
    int Year,
    int? Month = null) : IRequest<IEnumerable<InvoiceDto>>;
