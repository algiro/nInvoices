namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for updating an existing invoice template.
/// </summary>
public sealed record UpdateInvoiceTemplateDto(
    string Name,
    string Content,
    bool IsActive = true);