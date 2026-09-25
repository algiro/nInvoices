using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Invoices.Queries;

/// <summary>
/// Renders the invoice and timesheet that generating <paramref name="Invoice"/> would produce,
/// without saving anything, so they can be reviewed first.
/// </summary>
public sealed record PreviewInvoiceDraftQuery(GenerateInvoiceDto Invoice) : IRequest<InvoiceDraftPreviewDto>;
