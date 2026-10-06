using Mediator;
using nInvoices.Application.DTOs;
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
    private readonly IInvoiceFinalizer _finalizer;
    private readonly IUnitOfWork _unitOfWork;

    public BulkChangeInvoiceStatusCommandHandler(
        IInvoiceRepository repository,
        IRepository<Customer> customerRepository,
        IInvoiceFinalizer finalizer,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _customerRepository = customerRepository;
        _finalizer = finalizer;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<BulkInvoiceResultDto> Handle(BulkChangeInvoiceStatusCommand request, CancellationToken cancellationToken)
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

        var outdated = new List<long>();
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

                if (await _finalizer.FinalizeAsync(invoice, customer, cancellationToken))
                    outdated.Add(invoice.Id);
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

        if (request.Action == BulkInvoiceStatusAction.Finalize)
            await _finalizer.CompleteAsync(succeeded, outdated, cancellationToken);

        return new BulkInvoiceResultDto(succeeded, skipped);
    }

    /// <summary>
    /// Why the action doesn't apply to an invoice in this status; null when it does. The rules are
    /// the invoice's own (<see cref="InvoiceLifecycle"/>), so a bulk change allows exactly what a
    /// single one does.
    /// </summary>
    public static string? WhyNot(BulkInvoiceStatusAction action, InvoiceStatus status) =>
        InvoiceLifecycle.WhyNot(status, action switch
        {
            BulkInvoiceStatusAction.Finalize => InvoiceAction.Finalize,
            BulkInvoiceStatusAction.MarkAsSent => InvoiceAction.MarkAsSent,
            BulkInvoiceStatusAction.MarkAsPaid => InvoiceAction.MarkAsPaid,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        });

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
