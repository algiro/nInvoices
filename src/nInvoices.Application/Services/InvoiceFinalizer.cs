using Mediator;
using nInvoices.Application.Features.Invoices;
using nInvoices.Application.Features.Invoices.Notifications;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;

namespace nInvoices.Application.Services;

/// <summary>
/// Finalizing a draft, shared by the single and the bulk command so a new rule or step cannot
/// reach one path only. It runs in two halves around the caller's save:
/// <see cref="FinalizeAsync"/> before it, <see cref="CompleteAsync"/> after it.
/// </summary>
public interface IInvoiceFinalizer
{
    /// <summary>
    /// Gives the draft its number from the sequence and runs the <see cref="IInvoiceLifecycleStep"/>s,
    /// in memory: the caller saves the invoice and the sequence together, so a number is never used
    /// up without an invoice holding it.
    /// </summary>
    /// <returns>True when the stored document is out of date (the number or a step changed it).</returns>
    /// <exception cref="Core.Exceptions.DomainException">The invoice is not a draft.</exception>
    Task<bool> FinalizeAsync(Invoice invoice, Customer customer, CancellationToken cancellationToken = default);

    /// <summary>
    /// After the save: re-renders the out-of-date documents, moves the remaining drafts on to the
    /// next number and announces each finalized invoice.
    /// </summary>
    Task CompleteAsync(IReadOnlyCollection<long> finalizedIds, IReadOnlyCollection<long> outdatedIds, CancellationToken cancellationToken = default);
}

public sealed class InvoiceFinalizer : IInvoiceFinalizer
{
    private readonly IInvoiceNumbering _numbering;
    private readonly IDraftInvoiceSynchronizer _drafts;
    private readonly IPublisher _publisher;
    private readonly IReadOnlyList<IInvoiceLifecycleStep> _steps;

    public InvoiceFinalizer(
        IInvoiceNumbering numbering,
        IDraftInvoiceSynchronizer drafts,
        IPublisher publisher,
        IEnumerable<IInvoiceLifecycleStep>? steps = null)
    {
        _numbering = numbering;
        _drafts = drafts;
        _publisher = publisher;
        _steps = steps?.ToList() ?? [];
    }

    public async Task<bool> FinalizeAsync(Invoice invoice, Customer customer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(customer);

        // Throws unless the invoice is a draft, before a number is taken
        invoice.EnsureAllowed(InvoiceAction.Finalize);

        var number = await _numbering.TakeAsync(customer, invoice.IssueDate, cancellationToken);
        // The document of an up-to-date draft already carries this number
        var outdated = invoice.Number != number;
        invoice.Finalize(number);

        // What country rules require of an issued invoice is stored in the same save
        foreach (var step in _steps)
            outdated |= await step.OnFinalizingAsync(invoice, customer, cancellationToken);

        return outdated;
    }

    public async Task CompleteAsync(IReadOnlyCollection<long> finalizedIds, IReadOnlyCollection<long> outdatedIds, CancellationToken cancellationToken = default)
    {
        if (finalizedIds.Count == 0)
            return;

        if (outdatedIds.Count > 0)
            await _drafts.RerenderAsync(outdatedIds, cancellationToken);

        await _drafts.RefreshDraftsAsync(cancellationToken);

        foreach (var id in finalizedIds)
            await _publisher.Publish(new InvoiceFinalizedNotification(id), cancellationToken);
    }
}
