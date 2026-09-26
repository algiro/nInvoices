namespace nInvoices.Core.Entities;

/// <summary>
/// Represents the time allocated to a single project on a given work day.
/// A work day can be split across multiple projects (e.g. 3h on project A, 5h on project B).
/// </summary>
public sealed class WorkDayProject : OwnedEntityBase
{
    public long WorkDayId { get; set; }
    public long ProjectId { get; set; }
    public decimal Hours { get; set; }

    // Navigation properties
    public WorkDay WorkDay { get; set; } = null!;
    public Project Project { get; set; } = null!;

    public WorkDayProject()
    {
        CreatedAt = DateTime.UtcNow;
    }

    public WorkDayProject(long projectId, decimal hours) : this()
    {
        if (projectId <= 0)
            throw new ArgumentException("Project ID must be positive", nameof(projectId));
        if (hours <= 0)
            throw new ArgumentException("Hours must be positive", nameof(hours));

        ProjectId = projectId;
        Hours = hours;
    }

    /// <summary>
    /// Creates an allocation for a project that may not have been persisted yet.
    /// EF Core fills in <see cref="ProjectId"/> from the navigation property on save.
    /// </summary>
    public WorkDayProject(Project project, decimal hours) : this()
    {
        ArgumentNullException.ThrowIfNull(project);
        if (hours <= 0)
            throw new ArgumentException("Hours must be positive", nameof(hours));

        Project = project;
        ProjectId = project.Id;
        Hours = hours;
    }

    public void UpdateHours(decimal hours)
    {
        if (hours <= 0)
            throw new ArgumentException("Hours must be positive", nameof(hours));

        Hours = hours;
        UpdatedAt = DateTime.UtcNow;
    }
}
