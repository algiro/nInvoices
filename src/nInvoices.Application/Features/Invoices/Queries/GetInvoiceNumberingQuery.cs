using Mediator;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Invoices.Queries;

public sealed record GetInvoiceNumberingQuery : IRequest<InvoiceNumberingDto>;
