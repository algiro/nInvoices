using nInvoices.Application.Features.Invoices;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Compliance.Spain.Verifactu;

/// <summary>Records every invoice issued or cancelled in the user Verifactu chain, in the same save as the change.</summary>
public sealed class VerifactuLifecycleStep : IInvoiceLifecycleStep
{
    private readonly IVerifactuService _verifactu;

    public VerifactuLifecycleStep(IVerifactuService verifactu)
    {
        _verifactu = verifactu;
    }

    // The QR code and the legend now belong on the document
    public Task<bool> OnFinalizingAsync(Invoice invoice, Customer customer, CancellationToken cancellationToken) =>
        _verifactu.RecordIssuedAsync(invoice, customer, cancellationToken);

    public async Task OnCancellingAsync(Invoice invoice, CancellationToken cancellationToken) =>
        await _verifactu.RecordCancelledAsync(invoice, cancellationToken);

    public Task OnDeletingAsync(Invoice invoice, CancellationToken cancellationToken) =>
        _verifactu.EnsureCanDeleteAsync(invoice, cancellationToken);
}
