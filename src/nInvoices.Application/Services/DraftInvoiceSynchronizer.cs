using Microsoft.Extensions.Logging;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Services;

/// <summary>
/// Keeps the drafts in step with the sequence. Drafts do not use up a number, so all of them show
/// the same "next number"; it changes whenever an invoice is finalized (or the sequence or pattern
/// is edited), and then every draft's number and document has to follow.
/// </summary>
public interface IDraftInvoiceSynchronizer
{
    /// <summary>
    /// Gives every draft the number it shows now (the next one in the sequence) and re-renders the
    /// documents of those whose number changed. Does nothing when they are all up to date.
    /// </summary>
    Task RefreshDraftsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-renders the documents of these invoices, after their number changed. A document that
    /// cannot be rendered (say its template was deleted) is skipped and logged; it never fails the caller.
    /// </summary>
    Task RerenderAsync(IEnumerable<long> invoiceIds, CancellationToken cancellationToken = default);
}

public sealed class DraftInvoiceSynchronizer : IDraftInvoiceSynchronizer
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IInvoiceNumbering _numbering;
    private readonly IInvoiceGenerationService _generation;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DraftInvoiceSynchronizer> _logger;

    public DraftInvoiceSynchronizer(
        IInvoiceRepository invoiceRepository,
        IRepository<Customer> customerRepository,
        IInvoiceNumbering numbering,
        IInvoiceGenerationService generation,
        IUnitOfWork unitOfWork,
        ILogger<DraftInvoiceSynchronizer> logger)
    {
        _invoiceRepository = invoiceRepository;
        _customerRepository = customerRepository;
        _numbering = numbering;
        _generation = generation;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task RefreshDraftsAsync(CancellationToken cancellationToken = default)
    {
        var drafts = (await _invoiceRepository.FindAsync(i => i.Status == InvoiceStatus.Draft, cancellationToken)).ToList();

        var changed = new List<long>();
        foreach (var draft in drafts)
        {
            var customer = await _customerRepository.GetByIdAsync(draft.CustomerId, cancellationToken);
            if (customer == null)
                continue;

            var current = await _numbering.PeekAsync(customer, draft.IssueDate, cancellationToken);
            if (draft.Number == current)
                continue;

            draft.RenumberDraft(current);
            await _invoiceRepository.UpdateAsync(draft, cancellationToken);
            changed.Add(draft.Id);
        }

        if (changed.Count == 0)
            return;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RerenderAsync(changed, cancellationToken);
    }

    public async Task RerenderAsync(IEnumerable<long> invoiceIds, CancellationToken cancellationToken = default)
    {
        foreach (var id in invoiceIds)
        {
            try
            {
                await _generation.RegenerateInvoiceHtmlAsync(id, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "The document of invoice {InvoiceId} could not be re-rendered with its new number", id);
            }
        }
    }
}
