namespace nInvoices.Core.Enums;

/// <summary>
/// What can be done to an invoice, governed by <see cref="Entities.InvoiceLifecycle"/>. All but
/// <see cref="Delete"/> move it to another status; deleting removes it.
/// </summary>
public enum InvoiceAction
{
    Finalize,
    MarkAsSent,
    MarkAsPaid,
    Cancel,
    Delete
}
