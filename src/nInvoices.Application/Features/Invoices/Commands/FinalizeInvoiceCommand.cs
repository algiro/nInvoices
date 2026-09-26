using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Invoices.Commands;

/// <summary>
/// Command to finalize an invoice.
/// Changes status from Draft to Finalized, preventing further modifications.
/// </summary>
public sealed record FinalizeInvoiceCommand(long InvoiceId) : IRequest<Unit>;