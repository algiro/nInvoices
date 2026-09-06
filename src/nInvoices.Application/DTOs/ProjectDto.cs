namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for project information.
/// </summary>
public sealed record ProjectDto(
    long Id,
    long CustomerId,
    string Name,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
