using nInvoices.Core.Entities;

namespace nInvoices.Application.Features.Invoices;

/// <summary>
/// A step country rules add to the life of an invoice, run inside the same save as the change itself,
/// so the invoice and what the rules require of it (a Verifactu record, say) are stored together or not
/// at all. With no country turned on, a step does nothing.
/// </summary>
public interface IInvoiceLifecycleStep
{
    /// <summary>
    /// Runs when an invoice is being finalized, after it has taken its number and before it is saved.
    /// Throwing stops the finalization.
    /// </summary>
    /// <returns>True if the document of the invoice must now show something new (it is then re-rendered).</returns>
    Task<bool> OnFinalizingAsync(Invoice invoice, Customer customer, CancellationToken cancellationToken);

    /// <summary>Runs when an invoice is being cancelled, before it is saved. Throwing stops the cancellation.</summary>
    Task OnCancellingAsync(Invoice invoice, CancellationToken cancellationToken);

    /// <summary>Runs before an invoice is deleted. Throwing refuses the deletion.</summary>
    Task OnDeletingAsync(Invoice invoice, CancellationToken cancellationToken);
}
