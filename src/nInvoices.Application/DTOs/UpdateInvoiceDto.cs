namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for updating a draft invoice.
/// </summary>
public sealed record UpdateInvoiceDto(
    DateOnly? DueDate = null,
    string? RenderedContent = null);