namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for creating a new project.
/// </summary>
public sealed record CreateProjectDto(
    long CustomerId,
    string Name);
