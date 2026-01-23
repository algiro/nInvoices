using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.InvoiceTemplates.Queries;

/// <summary>
/// Query to get all templates for a specific customer.
/// </summary>
public sealed record GetTemplatesByCustomerIdQuery(long CustomerId) : IRequest<IEnumerable<InvoiceTemplateDto>>;