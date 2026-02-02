using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for creating a new invoice template.
/// </summary>
public sealed record CreateInvoiceTemplateDto(
    long CustomerId,
    InvoiceType InvoiceType,
    string Name,
    string Content);