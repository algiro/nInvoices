namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for an expense entry.
/// </summary>
public sealed class ExpenseDto
{
    public required string Description { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    /// <summary>When absent, the expense is dated the day it is recorded (the invoice is generated, the backup restored).</summary>
    public DateOnly? Date { get; init; }
}
