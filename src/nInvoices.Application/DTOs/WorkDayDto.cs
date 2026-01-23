namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for a worked day entry.
/// </summary>
public sealed record WorkDayDto(
    DateOnly Date,
    string? Notes = null);