using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for invoice information.
/// </summary>
public sealed record InvoiceDto(
    long Id,
    long CustomerId,
    InvoiceType Type,
    string InvoiceNumber,
    DateOnly IssueDate,
    DateOnly? DueDate,
    MoneyDto Subtotal,
    MoneyDto TotalExpenses,
    MoneyDto TotalTaxes,
    MoneyDto Total,
    InvoiceStatus Status,
    string? RenderedContent,
    DateTime CreatedAt,
    DateTime UpdatedAt);