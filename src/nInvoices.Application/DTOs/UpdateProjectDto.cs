namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for updating an existing project.
/// </summary>
public sealed record UpdateProjectDto(
    string Name,
    bool IsActive = true);
