using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.InvoiceTemplates.Queries;

/// <summary>
/// Query to get all templates for a specific customer, or, with a null customer, the templates
/// shared by all customers.
/// </summary>
public sealed record GetTemplatesByCustomerIdQuery(long? CustomerId) : IRequest<IEnumerable<InvoiceTemplateDto>>;