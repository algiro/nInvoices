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
}
