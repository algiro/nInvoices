using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Mappings;

/// <summary>
/// Maps domain entities to DTOs following Single Responsibility Principle.
/// Centralizes mapping logic to avoid duplication across query handlers.
/// </summary>
public static class InvoiceMapper
{
    public static InvoiceDto ToDto(Invoice invoice) => ToDto(invoice, includeContent: true);

    /// <summary>For lists: everything but the rendered HTML, which only the invoice page needs.</summary>
    public static InvoiceDto ToListItemDto(Invoice invoice) => ToDto(invoice, includeContent: false);

    private static InvoiceDto ToDto(Invoice invoice, bool includeContent)
    {
        return new InvoiceDto
        {
            Id = invoice.Id,
            CustomerId = invoice.CustomerId,
            Type = invoice.Type,
            InvoiceNumber = invoice.Number.ToString(),
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            WorkedDays = invoice.WorkedDays,
            Year = invoice.Year,
            Month = invoice.Month,
            MonthlyReportTemplateId = invoice.MonthlyReportTemplateId,
            Subtotal = new MoneyDto(invoice.Subtotal.Amount, invoice.Subtotal.Currency),
            TotalExpenses = new MoneyDto(invoice.TotalExpenses.Amount, invoice.TotalExpenses.Currency),
            TotalTaxes = new MoneyDto(invoice.TotalTaxes.Amount, invoice.TotalTaxes.Currency),
            Total = new MoneyDto(invoice.Total.Amount, invoice.Total.Currency),
            Status = invoice.Status,
            RenderedContent = includeContent ? invoice.RenderedContent : null,
            Notes = invoice.Notes,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
        };
    }
}
