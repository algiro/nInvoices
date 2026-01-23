namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for an expense entry.
/// </summary>
public sealed class ExpenseDto
{
    public required string Description { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public DateOnly Date { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);
}
