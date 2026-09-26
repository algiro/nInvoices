using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed class BulkChangeInvoiceStatusCommandHandler : IRequestHandler<BulkChangeInvoiceStatusCommand, BulkInvoiceResultDto>
{
    public const int MaxInvoices = 500;

    private readonly IInvoiceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public BulkChangeInvoiceStatusCommandHandler(IInvoiceRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<BulkInvoiceResultDto> Handle(BulkChangeInvoiceStatusCommand request, CancellationToken cancellationToken)
    {
        var ids = request.InvoiceIds.Distinct().ToList();
        if (ids.Count > MaxInvoices)
            throw new ArgumentException($"At most {MaxInvoices} invoices can be changed at once");

        var invoices = (await _repository.GetByIdsAsync(ids, cancellationToken)).ToDictionary(i => i.Id);
        var succeeded = new List<long>();
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

            Apply(request.Action, invoice);
            succeeded.Add(id);
        }

        if (succeeded.Count > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

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
            case BulkInvoiceStatusAction.Finalize:
                invoice.FinalizeInvoice();
                break;
            case BulkInvoiceStatusAction.MarkAsSent:
                invoice.MarkAsSent();
                break;
            case BulkInvoiceStatusAction.MarkAsPaid:
                invoice.MarkAsPaid();
                break;
        }
    }
}
