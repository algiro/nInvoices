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
    IReadOnlyList<MonthlyReportTemplateExportDto> MonthlyReportTemplates,
    string? Email = null,
    string? CcEmails = null,
    IReadOnlyList<EmailTemplateExportDto>? EmailTemplates = null,
    string? HolidayCountry = null);

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

public sealed record EmailTemplateExportDto(
    string Name,
    string Subject,
    string Body,
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
/// The templates shared by all of the user's customers (they belong to no customer).
/// </summary>
public sealed record SharedTemplatesExportDto(
    IReadOnlyList<InvoiceTemplateExportDto> InvoiceTemplates,
    IReadOnlyList<MonthlyReportTemplateExportDto> MonthlyReportTemplates,
    IReadOnlyList<EmailTemplateExportDto> EmailTemplates);

/// <summary>
/// Root export container with metadata. <see cref="SharedTemplates"/> is absent from exports
/// made before templates could be shared.
/// </summary>
public sealed record DataExportDto(
    string ExportVersion,
    DateTime ExportedAt,
    IReadOnlyList<CustomerExportDto>? Customers,
    IReadOnlyList<InvoiceExportDto>? Invoices,
    SharedTemplatesExportDto? SharedTemplates = null);
