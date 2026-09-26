using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Invoices.Commands;

/// <summary>A status change that can be applied to several invoices at once.</summary>
public enum BulkInvoiceStatusAction
{
    Finalize,
    MarkAsSent,
    MarkAsPaid
}

/// <summary>
/// Applies a status change to each invoice it is valid for; the others are skipped with the
/// reason, and nothing fails as a whole because of them.
/// </summary>
public sealed record BulkChangeInvoiceStatusCommand(BulkInvoiceStatusAction Action, IReadOnlyList<long> InvoiceIds)
    : IRequest<BulkInvoiceResultDto>;
