using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Command to update an existing invoice template.
/// </summary>
public sealed record UpdateInvoiceTemplateCommand(long Id, UpdateInvoiceTemplateDto Template) : IRequest<InvoiceTemplateDto>;