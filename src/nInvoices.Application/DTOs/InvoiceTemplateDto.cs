using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for invoice template information.
/// Templates define the HTML/text structure for invoice generation.
/// </summary>
public sealed record InvoiceTemplateDto(
    long Id,
    long CustomerId,
    InvoiceType InvoiceType,
    string Name,
    string Content,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);