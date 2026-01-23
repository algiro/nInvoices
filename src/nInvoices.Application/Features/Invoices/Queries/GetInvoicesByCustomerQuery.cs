using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Invoices.Queries;

public sealed record GetInvoicesByCustomerQuery(long CustomerId) : IRequest<IEnumerable<InvoiceDto>>;
