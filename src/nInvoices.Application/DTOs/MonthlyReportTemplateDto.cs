using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// DTO for monthly report template information.
/// </summary>
public sealed record MonthlyReportTemplateDto(
    long Id,
    long CustomerId,
    InvoiceType InvoiceType,
    string Name,
    string Content,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// DTO for creating a new monthly report template.
/// </summary>
public sealed record CreateMonthlyReportTemplateDto(
    long CustomerId,
    string Name,
    string Content,
    InvoiceType InvoiceType = InvoiceType.Monthly);

/// <summary>
/// DTO for updating an existing monthly report template.
/// </summary>
public sealed record UpdateMonthlyReportTemplateDto(
    string Name,
    string Content);

/// <summary>
/// DTO for validating monthly report template content.
/// </summary>
public sealed record ValidateMonthlyReportTemplateDto(
    string Content);
