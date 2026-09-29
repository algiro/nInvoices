using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed record UpdateInvoiceNumberingCommand(UpdateInvoiceNumberingDto Numbering) : IRequest<InvoiceNumberingDto>;
