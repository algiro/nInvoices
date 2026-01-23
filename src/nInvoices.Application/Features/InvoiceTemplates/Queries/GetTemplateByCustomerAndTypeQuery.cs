using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Enums;

namespace nInvoices.Application.Features.InvoiceTemplates.Queries;

/// <summary>
/// Query to get template for specific customer and invoice type.
/// Essential for invoice generation - finds the right template to use.
/// </summary>
public sealed record GetTemplateByCustomerAndTypeQuery(
    long CustomerId, 
    InvoiceType Type) : IRequest<InvoiceTemplateDto?>;