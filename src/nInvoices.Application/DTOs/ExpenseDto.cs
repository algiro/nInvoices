namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for an expense entry.
/// </summary>
public sealed record ExpenseDto(
    string Description,
    MoneyDto Amount);