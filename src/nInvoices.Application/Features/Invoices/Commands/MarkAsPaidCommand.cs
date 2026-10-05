using Mediator;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed record MarkAsPaidCommand(long InvoiceId) : IRequest<Unit>;
