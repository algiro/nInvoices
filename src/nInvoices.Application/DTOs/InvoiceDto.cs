using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for invoice information.
/// </summary>
public sealed class InvoiceDto
{
    public long Id { get; init; }
    public long CustomerId { get; init; }
    public InvoiceType Type { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public DateOnly IssueDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public int? WorkedDays { get; init; }
    public int? Year { get; init; }
    public int? Month { get; init; }
    public long? MonthlyReportTemplateId { get; init; }
    public MoneyDto Subtotal { get; init; } = null!;
    public MoneyDto TotalExpenses { get; init; } = null!;
    public MoneyDto TotalTaxes { get; init; } = null!;
    public MoneyDto Total { get; init; } = null!;
    public InvoiceStatus Status { get; init; }
    public string? RenderedContent { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
