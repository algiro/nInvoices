using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Mappings;

/// <summary>
/// Maps <see cref="Project"/> entities to DTOs.
/// Centralizes mapping logic to avoid duplication across query and command handlers.
/// </summary>
public static class ProjectMapper
{
    public static ProjectDto ToDto(Project project) =>
        new(
            project.Id,
            project.CustomerId,
            project.Name,
            project.IsActive,
            project.CreatedAt,
            project.UpdatedAt ?? project.CreatedAt);
}
