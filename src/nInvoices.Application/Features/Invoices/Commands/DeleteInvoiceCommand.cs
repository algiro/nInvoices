using MediatR;

namespace nInvoices.Application.Features.Invoices.Commands;

/// <summary>
/// Command to delete a draft invoice.
/// Only draft invoices can be deleted.
/// </summary>
public sealed record DeleteInvoiceCommand(long Id) : IRequest<bool>;