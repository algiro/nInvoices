using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Invoices.Queries;

/// <summary>Outstanding and paid totals, drafts and invoice years, over all invoices.</summary>
public sealed record GetInvoiceSummaryQuery(int CurrentYear) : IRequest<InvoiceSummaryDto>;
