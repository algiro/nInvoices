using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for generating a new invoice.
/// Contains all information needed for invoice creation.
/// </summary>
public sealed class GenerateInvoiceDto
{
    public required long CustomerId { get; init; }
    public required InvoiceType InvoiceType { get; init; }
    public DateOnly IssueDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public int? Year { get; init; }
    public int? Month { get; init; }
    public ICollection<WorkDayDto>? WorkDays { get; init; }
    public ICollection<ExpenseDto>? Expenses { get; init; }
    public string InvoiceNumberFormat { get; init; } = "INV-{YEAR}-{NUMBER:000}";
    public long? MonthlyReportTemplateId { get; init; }

    /// <summary>The customer's rate to bill with; null to use the default (Daily, then Monthly, then Hourly).</summary>
    public long? RateId { get; init; }

    /// <summary>Hours to bill on a one-time invoice with an hourly rate (hours × rate).</summary>
    public decimal? Hours { get; init; }
}
