using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Invoices.Notifications;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed class BulkChangeInvoiceStatusCommandHandler : IRequestHandler<BulkChangeInvoiceStatusCommand, BulkInvoiceResultDto>
{
    public const int MaxInvoices = 500;

    private readonly IInvoiceRepository _repository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IInvoiceNumbering _numbering;
    private readonly IDraftInvoiceSynchronizer _drafts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public BulkChangeInvoiceStatusCommandHandler(
        IInvoiceRepository repository,
        IRepository<Customer> customerRepository,
        IInvoiceNumbering numbering,
        IDraftInvoiceSynchronizer drafts,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _repository = repository;
        _customerRepository = customerRepository;
        _numbering = numbering;
        _drafts = drafts;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<BulkInvoiceResultDto> Handle(BulkChangeInvoiceStatusCommand request, CancellationToken cancellationToken)
    {
        var ids = request.InvoiceIds.Distinct().ToList();
        if (ids.Count > MaxInvoices)
            throw new ArgumentException($"At most {MaxInvoices} invoices can be changed at once");

        var invoices = (await _repository.GetByIdsAsync(ids, cancellationToken)).ToDictionary(i => i.Id);
        var eligible = new List<Invoice>();
        var skipped = new List<BulkInvoiceSkipDto>();

        foreach (var id in ids)
        {
            if (!invoices.TryGetValue(id, out var invoice))
            {
                skipped.Add(new BulkInvoiceSkipDto(id, null, "Not found"));
                continue;
            }

            var reason = WhyNot(request.Action, invoice.Status);
            if (reason is not null)
            {
                skipped.Add(new BulkInvoiceSkipDto(id, invoice.Number.ToString(), reason));
                continue;
            }

            eligible.Add(invoice);
        }

        var renumbered = new List<long>();
        if (request.Action == BulkInvoiceStatusAction.Finalize)
        {
            // Numbers follow the invoice dates: the earliest issue date gets the lowest number
            foreach (var invoice in eligible.OrderBy(i => i.IssueDate).ThenBy(i => i.CreatedAt).ThenBy(i => i.Id))
            {
                var customer = await _customerRepository.GetByIdAsync(invoice.CustomerId, cancellationToken);
                if (customer == null)
                {
                    skipped.Add(new BulkInvoiceSkipDto(invoice.Id, invoice.Number.ToString(), "Customer not found"));
                    continue;
                }

                invoice.FinalizeInvoice();
                var number = await _numbering.TakeAsync(customer, invoice.IssueDate, cancellationToken);
                if (invoice.Number != number)
                    renumbered.Add(invoice.Id);
                invoice.Number = number;
            }
        }
        else
        {
            foreach (var invoice in eligible)
                Apply(request.Action, invoice);
        }

        var skippedIds = skipped.Select(s => s.Id).ToHashSet();
        var succeeded = eligible.Where(i => !skippedIds.Contains(i.Id)).Select(i => i.Id).ToList();

        if (succeeded.Count > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (request.Action == BulkInvoiceStatusAction.Finalize && succeeded.Count > 0)
        {
            await _drafts.RerenderAsync(renumbered, cancellationToken);
            await _drafts.RefreshDraftsAsync(cancellationToken);

            foreach (var id in succeeded)
                await _publisher.Publish(new InvoiceFinalizedNotification(id), cancellationToken);
        }

        return new BulkInvoiceResultDto(succeeded, skipped);
    }

    /// <summary>
    /// Why the action doesn't apply to an invoice in this status; null when it does. Follows the
    /// lifecycle the invoice screens offer (Draft → Finalized → Sent → Paid), so a draft is not
    /// marked as paid even though the entity would allow it.
    /// </summary>
    public static string? WhyNot(BulkInvoiceStatusAction action, InvoiceStatus status) => (action, status) switch
    {
        (BulkInvoiceStatusAction.Finalize, InvoiceStatus.Draft) => null,
        (BulkInvoiceStatusAction.Finalize, _) => "Already finalized",

        (BulkInvoiceStatusAction.MarkAsSent, InvoiceStatus.Finalized) => null,
        (BulkInvoiceStatusAction.MarkAsSent, InvoiceStatus.Draft) => "Not finalized yet",
        (BulkInvoiceStatusAction.MarkAsSent, InvoiceStatus.Cancelled) => "Cancelled",
        (BulkInvoiceStatusAction.MarkAsSent, _) => "Already sent",

        (BulkInvoiceStatusAction.MarkAsPaid, InvoiceStatus.Finalized or InvoiceStatus.Sent) => null,
        (BulkInvoiceStatusAction.MarkAsPaid, InvoiceStatus.Draft) => "Not finalized yet",
        (BulkInvoiceStatusAction.MarkAsPaid, InvoiceStatus.Paid) => "Already paid",
        (BulkInvoiceStatusAction.MarkAsPaid, _) => "Cancelled",

        _ => "Not supported"
    };

    private static void Apply(BulkInvoiceStatusAction action, Invoice invoice)
    {
        switch (action)
        {
            case BulkInvoiceStatusAction.MarkAsSent:
                invoice.MarkAsSent();
                break;
            case BulkInvoiceStatusAction.MarkAsPaid:
                invoice.MarkAsPaid();
                break;
        }
    }
}
