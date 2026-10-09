using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for invoice information.
/// </summary>
public sealed class InvoiceDto
{
    public required long Id { get; init; }
    public required long CustomerId { get; init; }
    public required InvoiceType Type { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly IssueDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public int? WorkedDays { get; init; }
    public int? Year { get; init; }
    public int? Month { get; init; }
    public long? MonthlyReportTemplateId { get; init; }
    public required MoneyDto Subtotal { get; init; }
    public required MoneyDto TotalExpenses { get; init; }
    public required MoneyDto TotalTaxes { get; init; }
    public required MoneyDto Total { get; init; }
    public required InvoiceStatus Status { get; init; }
    public string? RenderedContent { get; init; }
    public string? Notes { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
