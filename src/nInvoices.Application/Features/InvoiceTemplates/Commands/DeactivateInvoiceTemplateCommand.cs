using MediatR;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Command to deactivate an invoice template.
/// </summary>
public sealed record DeactivateInvoiceTemplateCommand(long Id) : IRequest<bool>;
