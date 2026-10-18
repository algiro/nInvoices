using nInvoices.Application.Features.Invoices;
using nInvoices.Application.Localization;
using nInvoices.Core.Entities;
using nInvoices.Core.Exceptions;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Compliance.EInvoice;

/// <summary>An invoice whose e-invoice was delivered to a channel (FACe...) stays: the delivery is on record there.</summary>
public sealed class EInvoiceDeliveryLifecycleStep : IInvoiceLifecycleStep
{
    private readonly IRepository<InvoiceEInvoice> _files;
    private readonly IRepository<EInvoiceSubmission> _submissions;
    private readonly IAppText _text;

    public EInvoiceDeliveryLifecycleStep(IRepository<InvoiceEInvoice> files, IRepository<EInvoiceSubmission> submissions, IAppText text)
    {
        _files = files;
        _submissions = submissions;
        _text = text;
    }

    public Task<bool> OnFinalizingAsync(Invoice invoice, Customer customer, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task OnCancellingAsync(Invoice invoice, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task OnDeletingAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var ids = (await _files.FindAsync(f => f.InvoiceId == invoice.Id, cancellationToken)).Select(f => f.Id).ToList();
        if (ids.Count == 0)
            return;

        var delivered = (await _submissions.FindAsync(s => ids.Contains(s.InvoiceEInvoiceId), cancellationToken)).FirstOrDefault();
        if (delivered is not null)
            throw new DomainException(
                _text.Translate("errors.einvoice.sentCannotDelete", ("number", invoice.Number), ("channel", delivered.ChannelId.ToUpperInvariant()), ("reference", delivered.Reference)));
    }
}
