namespace nInvoices.Core.Enums;

/// <summary>
/// Defines the status of an invoice.
/// </summary>
public enum InvoiceStatus
{
    Draft,
    Finalized,
    Sent,
    Paid,
    Cancelled
}
