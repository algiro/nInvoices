using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Command to create a new invoice template for a customer.
/// Templates define the structure and layout of generated invoices.
/// </summary>
public sealed record CreateInvoiceTemplateCommand(CreateInvoiceTemplateDto Template) : IRequest<InvoiceTemplateDto>;