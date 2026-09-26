using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Enforces "one active template per customer and invoice type": activating a template
/// deactivates the one that was active before, in the same transaction.
/// </summary>
internal static class InvoiceTemplateActivation
{
    /// <summary>
    /// Makes <paramref name="template"/> the active one for its customer and type and saves.
    /// The previous active template is saved as inactive first, so the unique index on active
    /// templates never sees two at once (SQLite checks it row by row).
    /// </summary>
    public static async Task ActivateAsync(
        InvoiceTemplate template,
        IRepository<InvoiceTemplate> repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var previouslyActive = await repository.FindAsync(
                t => t.CustomerId == template.CustomerId
                     && t.InvoiceType == template.InvoiceType
                     && t.IsActive
                     && t.Id != template.Id,
                cancellationToken);

            foreach (var other in previouslyActive)
            {
                other.Deactivate();
                await repository.UpdateAsync(other, cancellationToken);
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);

            template.Activate();
            await repository.UpdateAsync(template, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
