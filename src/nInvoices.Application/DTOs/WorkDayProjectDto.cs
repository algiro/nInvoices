namespace nInvoices.Application.DTOs;

/// <summary>
/// Time allocated to a project on a single work day.
/// <see cref="ProjectId"/> is set when an existing project was picked; otherwise
/// <see cref="ProjectName"/> is resolved to an existing project or a new one is created.
/// </summary>
public sealed record WorkDayProjectDto(
    string ProjectName,
    decimal Hours,
    long? ProjectId = null);
