using MediatR;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Command to activate an invoice template.
/// </summary>
public sealed record ActivateInvoiceTemplateCommand(long Id) : IRequest<bool>;
