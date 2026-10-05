using nInvoices.Core.Enums;

namespace nInvoices.Core.Entities;

/// <summary>
/// The one place that says what can be done to an invoice in each status:
/// <code>
///  (deleted) ◄──Delete── Draft ──Finalize──► Finalized ──MarkAsSent──► Sent
///                                                │  └──MarkAsPaid─────────┤──MarkAsPaid──► Paid
///                                                └──Cancel────────────────┴──Cancel──────► Cancelled
/// </code>
/// A draft has no number of its own yet: nothing was issued, so it is deleted, never cancelled. An
/// issued invoice keeps its number on record: it is cancelled, never deleted (except by an explicit
/// forced delete, which bypasses this table). Paid and Cancelled are final. <see cref="Invoice"/>
/// enforces this table, the delete handler checks it, and the bulk actions use it to explain why an
/// invoice was skipped.
/// </summary>
public static class InvoiceLifecycle
{
    /// <summary>Why <paramref name="action"/> is not allowed for an invoice in <paramref name="status"/>; null when it is.</summary>
    public static string? WhyNot(InvoiceStatus status, InvoiceAction action) => (action, status) switch
    {
        (InvoiceAction.Finalize, InvoiceStatus.Draft) => null,
        (InvoiceAction.Finalize, _) => "Already finalized",

        (InvoiceAction.MarkAsSent, InvoiceStatus.Finalized) => null,
        (InvoiceAction.MarkAsSent, InvoiceStatus.Draft) => "Not finalized yet",
        (InvoiceAction.MarkAsSent, InvoiceStatus.Sent) => "Already sent",
        (InvoiceAction.MarkAsSent, InvoiceStatus.Paid) => "Already paid",
        (InvoiceAction.MarkAsSent, InvoiceStatus.Cancelled) => "Cancelled",

        (InvoiceAction.MarkAsPaid, InvoiceStatus.Finalized or InvoiceStatus.Sent) => null,
        (InvoiceAction.MarkAsPaid, InvoiceStatus.Draft) => "Not finalized yet",
        (InvoiceAction.MarkAsPaid, InvoiceStatus.Paid) => "Already paid",
        (InvoiceAction.MarkAsPaid, InvoiceStatus.Cancelled) => "Cancelled",

        (InvoiceAction.Cancel, InvoiceStatus.Finalized or InvoiceStatus.Sent) => null,
        (InvoiceAction.Cancel, InvoiceStatus.Draft) => "Not finalized yet (delete the draft instead)",
        (InvoiceAction.Cancel, InvoiceStatus.Paid) => "Already paid",
        (InvoiceAction.Cancel, InvoiceStatus.Cancelled) => "Already cancelled",

        (InvoiceAction.Delete, InvoiceStatus.Draft) => null,
        (InvoiceAction.Delete, InvoiceStatus.Finalized or InvoiceStatus.Sent) => "Already finalized (cancel it instead)",
        (InvoiceAction.Delete, InvoiceStatus.Paid) => "Already paid",
        (InvoiceAction.Delete, InvoiceStatus.Cancelled) => "Already cancelled: it stays on record",

        _ => "Not supported"
    };

    public static bool Allows(InvoiceStatus status, InvoiceAction action) => WhyNot(status, action) is null;

    /// <summary>Whether <paramref name="action"/> moves the invoice to another status (all but <see cref="InvoiceAction.Delete"/>).</summary>
    public static bool ChangesStatus(InvoiceAction action) => action != InvoiceAction.Delete;

    /// <summary>The status an allowed <paramref name="action"/> leads to.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="action"/> doesn't change the status (<see cref="InvoiceAction.Delete"/>).</exception>
    public static InvoiceStatus Target(InvoiceAction action) => action switch
    {
        InvoiceAction.Finalize => InvoiceStatus.Finalized,
        InvoiceAction.MarkAsSent => InvoiceStatus.Sent,
        InvoiceAction.MarkAsPaid => InvoiceStatus.Paid,
        InvoiceAction.Cancel => InvoiceStatus.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Deleting removes the invoice: it leads to no status")
    };
}
