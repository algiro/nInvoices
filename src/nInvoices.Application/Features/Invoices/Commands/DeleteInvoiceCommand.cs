using MediatR;

namespace nInvoices.Application.Features.Invoices.Commands;

/// <summary>
/// Command to delete an invoice.
/// By default, only draft invoices can be deleted.
/// Use Force=true to delete any invoice regardless of status.
/// </summary>
public sealed record DeleteInvoiceCommand(long InvoiceId, bool Force = false) : IRequest<Unit>;