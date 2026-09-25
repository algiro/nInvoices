namespace nInvoices.Application.DTOs;

/// <summary>
/// The invoice and timesheet an in-progress <see cref="GenerateInvoiceDto"/> would produce,
/// rendered with the real data but not saved.
/// </summary>
/// <param name="InvoiceNumber">The number the invoice gets if it is generated now.</param>
/// <param name="InvoiceHtml">The rendered invoice, or null when it cannot be generated.</param>
/// <param name="Errors">Why the invoice cannot be generated (no rate, no active template, a template error).</param>
/// <param name="TimesheetHtml">The rendered timesheet for a monthly invoice, otherwise null.</param>
/// <param name="TimesheetError">Why the timesheet cannot be rendered; it does not block generating the invoice.</param>
public sealed record InvoiceDraftPreviewDto(
    string? InvoiceNumber,
    string? InvoiceHtml,
    IReadOnlyList<string> Errors,
    string? TimesheetHtml,
    string? TimesheetError,
    MoneyDto? Subtotal,
    MoneyDto? TotalExpenses,
    MoneyDto? TotalTaxes,
    MoneyDto? Total,
    IReadOnlyList<InvoiceDraftTaxLineDto> Taxes);

public sealed record InvoiceDraftTaxLineDto(string Description, decimal Rate, MoneyDto Amount);
