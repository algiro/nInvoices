using Mediator;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed record CancelInvoiceCommand(long InvoiceId) : IRequest<Unit>;
