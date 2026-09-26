using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Invoices.Queries;

public sealed record GetInvoiceByIdQuery(long InvoiceId) : IRequest<InvoiceDto?>;
