namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for monetary values.
/// Represents amount with currency.
/// </summary>
public sealed record MoneyDto(
    decimal Amount,
    string Currency);
