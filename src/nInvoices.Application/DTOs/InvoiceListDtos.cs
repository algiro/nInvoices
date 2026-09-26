using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.DTOs;

/// <summary>Filters, sort and page of the invoice list, as query-string parameters.</summary>
public sealed class InvoiceSearchDto
{
    public InvoiceStatus? Status { get; init; }
    public long? CustomerId { get; init; }
    public InvoiceType? Type { get; init; }
    public int? Year { get; init; }

    /// <summary>Part of the invoice number or of the customer name.</summary>
    public string? Search { get; init; }

    public InvoiceSortField Sort { get; init; } = InvoiceSortField.IssueDate;

    /// <summary>"asc" or "desc" (the default).</summary>
    public string? Dir { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

/// <summary>One page of the invoice list.</summary>
/// <param name="StatusCounts">Matching invoices per status, under every filter except the status one.</param>
public sealed record InvoicePageDto(
    IReadOnlyList<InvoiceDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyDictionary<InvoiceStatus, int> StatusCounts);

/// <summary>A number of invoices and their totals, one per currency.</summary>
public sealed record InvoiceAmountDto(int Count, IReadOnlyList<MoneyDto> Totals);

/// <summary>Figures shown above the invoice list, over all invoices.</summary>
/// <param name="Outstanding">Finalized or sent, not paid yet.</param>
/// <param name="PaidThisYear">Paid invoices issued this year.</param>
/// <param name="Years">Years that have invoices, newest first, for the year filter.</param>
public sealed record InvoiceSummaryDto(
    int TotalCount,
    InvoiceAmountDto Outstanding,
    InvoiceAmountDto PaidThisYear,
    int Drafts,
    IReadOnlyList<int> Years);

/// <summary>The invoices a bulk action applies to.</summary>
public sealed record BulkInvoiceIdsDto(IReadOnlyList<long> Ids);

/// <summary>An invoice a bulk action left unchanged, and why.</summary>
public sealed record BulkInvoiceSkipDto(long Id, string? InvoiceNumber, string Reason);

/// <summary>Outcome of a bulk action: the invoices it changed and the ones it skipped.</summary>
public sealed record BulkInvoiceResultDto(IReadOnlyList<long> Succeeded, IReadOnlyList<BulkInvoiceSkipDto> Skipped);

/// <summary>Invoices to download together as a zip.</summary>
/// <param name="IncludeMonthlyReports">Add the timesheet of each monthly invoice.</param>
public sealed record BulkInvoiceDownloadDto(IReadOnlyList<long> Ids, bool IncludeMonthlyReports = false);
