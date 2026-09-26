namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for updating a draft invoice.
/// </summary>
public sealed class UpdateInvoiceDto
{
    public DateOnly? DueDate { get; init; }
    public string? RenderedContent { get; init; }
    public string? Notes { get; init; }
}
