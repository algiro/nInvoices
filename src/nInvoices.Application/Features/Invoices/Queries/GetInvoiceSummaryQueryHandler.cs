using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Queries;

public sealed class GetInvoiceSummaryQueryHandler : IRequestHandler<GetInvoiceSummaryQuery, InvoiceSummaryDto>
{
    private readonly IInvoiceRepository _repository;

    public GetInvoiceSummaryQueryHandler(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<InvoiceSummaryDto> Handle(GetInvoiceSummaryQuery request, CancellationToken cancellationToken)
    {
        var rows = await _repository.GetTotalsAsync(cancellationToken);

        return new InvoiceSummaryDto(
            rows.Count,
            Amount(rows.Where(r => r.Status is InvoiceStatus.Finalized or InvoiceStatus.Sent)),
            Amount(rows.Where(r => r.Status == InvoiceStatus.Paid && r.IssueDate.Year == request.CurrentYear)),
            rows.Count(r => r.Status == InvoiceStatus.Draft),
            rows.Select(r => r.IssueDate.Year).Distinct().OrderDescending().ToList());
    }

    // Kept per currency: adding EUR to USD would be meaningless
    private static InvoiceAmountDto Amount(IEnumerable<InvoiceTotalsRow> rows)
    {
        var list = rows.ToList();
        return new InvoiceAmountDto(
            list.Count,
            list.GroupBy(r => r.Currency)
                .OrderBy(g => g.Key)
                .Select(g => new MoneyDto(g.Sum(r => r.Total), g.Key))
                .ToList());
    }
}
