using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Complete customer export with all related data.
/// </summary>
public sealed record CustomerExportDto(
    string Name,
    string FiscalId,
    AddressDto Address,
    DateTime CreatedAt,
    IReadOnlyList<RateExportDto> Rates,
    IReadOnlyList<TaxExportDto> Taxes,
    IReadOnlyList<InvoiceTemplateExportDto> InvoiceTemplates,
    IReadOnlyList<MonthlyReportTemplateExportDto> MonthlyReportTemplates);

public sealed record RateExportDto(
    RateType Type,
    MoneyDto Price,
    DateTime CreatedAt);

public sealed record TaxExportDto(
    string TaxId,
    string Description,
    string HandlerId,
    decimal Rate,
    TaxApplicationType ApplicationType,
    string? AppliedToTaxId,
    int Order,
    bool IsActive,
    DateTime CreatedAt);

public sealed record InvoiceTemplateExportDto(
    InvoiceType InvoiceType,
    string Name,
    string Content,
    bool IsActive,
    DateTime CreatedAt);

public sealed record MonthlyReportTemplateExportDto(
    InvoiceType InvoiceType,
    string Name,
    string Content,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>
/// Complete invoice export with line items and expenses.
/// </summary>
public sealed record InvoiceExportDto(
    string CustomerFiscalId,
    InvoiceType Type,
    string InvoiceNumber,
    DateOnly IssueDate,
    DateOnly? DueDate,
    int? WorkedDays,
    int? Year,
    int? Month,
    MoneyDto Subtotal,
    MoneyDto TotalExpenses,
    MoneyDto TotalTaxes,
    MoneyDto Total,
    InvoiceStatus Status,
    string? RenderedContent,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<ExpenseDto> Expenses,
    IReadOnlyList<InvoiceTaxLineExportDto> TaxLines);

public sealed record InvoiceTaxLineExportDto(
    string TaxId,
    string Description,
    decimal Rate,
    decimal BaseAmount,
    decimal TaxAmount,
    int Order);

/// <summary>
/// Root export container with metadata.
/// </summary>
public sealed record DataExportDto(
    string ExportVersion,
    DateTime ExportedAt,
    IReadOnlyList<CustomerExportDto>? Customers,
    IReadOnlyList<InvoiceExportDto>? Invoices);
