using MediatR;
using nInvoices.Application.Features.Invoices.Notifications;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Commands;

/// <summary>
/// Finalizes a draft. This is the moment it takes its number from the sequence; the other drafts
/// then move on to the next number.
/// </summary>
public sealed class FinalizeInvoiceCommandHandler : IRequestHandler<FinalizeInvoiceCommand, Unit>
{
    private readonly IRepository<Invoice> _repository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IInvoiceNumbering _numbering;
    private readonly IDraftInvoiceSynchronizer _drafts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly IReadOnlyList<IInvoiceLifecycleStep> _steps;

    public FinalizeInvoiceCommandHandler(
        IRepository<Invoice> repository,
        IRepository<Customer> customerRepository,
        IInvoiceNumbering numbering,
        IDraftInvoiceSynchronizer drafts,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        IEnumerable<IInvoiceLifecycleStep>? steps = null)
    {
        _steps = steps?.ToList() ?? [];
        _repository = repository;
        _customerRepository = customerRepository;
        _numbering = numbering;
        _drafts = drafts;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<Unit> Handle(FinalizeInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice == null)
            throw new InvalidOperationException($"Invoice with ID {request.InvoiceId} not found");

        var customer = await _customerRepository.GetByIdAsync(invoice.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer {invoice.CustomerId} not found");

        // Throws unless the invoice is a draft, before a number is taken
        invoice.FinalizeInvoice();

        // The invoice and the sequence are saved together: a number is never used up without an invoice holding it
        var number = await _numbering.TakeAsync(customer, invoice.IssueDate, cancellationToken);
        var numberChanged = invoice.Number != number;
        invoice.Number = number;

        // What country rules require of an issued invoice is stored in the same save
        var documentChanged = false;
        foreach (var step in _steps)
            documentChanged |= await step.OnFinalizingAsync(invoice, customer, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The document of an up-to-date draft already carries this number
        if (numberChanged || documentChanged)
            await _drafts.RerenderAsync([invoice.Id], cancellationToken);

        await _drafts.RefreshDraftsAsync(cancellationToken);

        await _publisher.Publish(new InvoiceFinalizedNotification(invoice.Id), cancellationToken);

        return Unit.Value;
    }
}
