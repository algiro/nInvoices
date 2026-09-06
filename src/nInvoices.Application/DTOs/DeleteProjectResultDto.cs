namespace nInvoices.Application.DTOs;

/// <summary>
/// Result of a project delete request.
/// A project referenced by existing work days is deactivated (soft delete) rather than removed.
/// </summary>
public sealed record DeleteProjectResultDto(
    bool Found,
    bool Deleted,
    bool Deactivated);
