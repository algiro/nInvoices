using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for invoice template information.
/// Templates define the HTML/text structure for invoice generation.
/// <see cref="CustomerId"/> is null for a template shared by all of the user's customers.
/// </summary>
public sealed record InvoiceTemplateDto(
    long Id,
    long? CustomerId,
    InvoiceType InvoiceType,
    string Name,
    string Content,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);