using Mediator;

namespace nInvoices.Application.Features.Invoices.Notifications;

/// <summary>
/// Published once an invoice has been finalized and saved (it has its number). Country compliance
/// steps that must follow every finalized invoice listen to it; they must not throw, since the
/// invoice is already final.
/// </summary>
public sealed record InvoiceFinalizedNotification(long InvoiceId) : INotification;
