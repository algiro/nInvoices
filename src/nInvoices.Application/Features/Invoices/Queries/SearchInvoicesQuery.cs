using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Invoices.Queries;

/// <summary>One page of the invoice list, filtered and sorted in the database.</summary>
public sealed record SearchInvoicesQuery(InvoiceSearchDto Search) : IRequest<InvoicePageDto>;
