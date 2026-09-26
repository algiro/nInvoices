using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.InvoiceTemplates.Queries;

public sealed record GetAllInvoiceTemplatesQuery : IRequest<IEnumerable<InvoiceTemplateDto>>;