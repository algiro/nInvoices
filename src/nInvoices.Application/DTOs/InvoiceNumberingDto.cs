namespace nInvoices.Application.DTOs;

/// <summary>
/// How the current user's invoices are numbered. <see cref="NumberFormat"/> is the pattern that
/// applies (the user's own, else the default); <see cref="CustomNumberFormat"/> is null when the
/// default is used. <see cref="NextNumber"/> is what the next invoice, issued today, would get
/// (with a sample customer code for the <c>{CUSTOMER}</c> tokens).
/// </summary>
public sealed record InvoiceNumberingDto(
    int CurrentValue,
    string NumberFormat,
    string? CustomNumberFormat,
    string DefaultNumberFormat,
    string NextNumber);

/// <summary>
/// Replaces the user's numbering. A null or blank <see cref="NumberFormat"/> goes back to the
/// default pattern. WARNING: a <see cref="Value"/> lower than the current one can cause duplicate
/// invoice numbers.
/// </summary>
public sealed record UpdateInvoiceNumberingDto(int Value, string? NumberFormat);
