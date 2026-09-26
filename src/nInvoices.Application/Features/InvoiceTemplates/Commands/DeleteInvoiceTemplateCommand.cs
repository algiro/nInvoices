using MediatR;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Command to delete an invoice template.
/// </summary>
public sealed record DeleteInvoiceTemplateCommand(long Id) : IRequest<bool>;