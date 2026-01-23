using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.InvoiceTemplates.Queries;

public sealed record GetInvoiceTemplateByIdQuery(long Id) : IRequest<InvoiceTemplateDto?>;