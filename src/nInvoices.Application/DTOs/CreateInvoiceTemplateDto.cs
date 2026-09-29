using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for creating a new invoice template.
/// A null <see cref="CustomerId"/> creates a template shared by all of the user's customers.
/// </summary>
public sealed record CreateInvoiceTemplateDto(
    long? CustomerId,
    InvoiceType InvoiceType,
    string Name,
    string Content);