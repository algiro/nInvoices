using nInvoices.Core.Enums;

namespace nInvoices.Core.Interfaces;

/// <summary>Columns the invoice list can be sorted by.</summary>
public enum InvoiceSortField
{
    IssueDate,
    Number,
    Customer,
    Period,
    Status,
    Total
}

/// <summary>
/// Filters, sort order and page for the invoice list. Null filters don't restrict.
/// </summary>
/// <param name="Search">Part of the invoice number or of the customer name, case-insensitive.</param>
/// <param name="Year">Year of the issue date.</param>
/// <param name="Page">1-based.</param>
public sealed record InvoiceSearchCriteria(
    InvoiceStatus? Status = null,
    long? CustomerId = null,
    InvoiceType? Type = null,
    int? Year = null,
    string? Search = null,
    InvoiceSortField Sort = InvoiceSortField.IssueDate,
    bool Descending = true,
    int Page = 1,
    int PageSize = 25);

/// <summary>One page of the invoice list and how many invoices match in total.</summary>
public sealed record InvoiceSearchResult(IReadOnlyList<Entities.Invoice> Items, int TotalCount);

/// <summary>The fields of an invoice the list's totals are computed from.</summary>
public sealed record InvoiceTotalsRow(InvoiceStatus Status, DateOnly IssueDate, string Currency, decimal Total);
