using Mediator;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed record MarkAsSentCommand(long InvoiceId) : IRequest<Unit>;
