using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Mappings;

public static class WorkDayMapper
{
    /// <summary>A saved work day; its project allocations must be loaded.</summary>
    public static WorkDayDto ToDto(WorkDay workDay) => new(
        workDay.Date,
        workDay.DayType,
        workDay.HoursWorked,
        workDay.Notes,
        workDay.Projects
            .Select(p => new WorkDayProjectDto(p.Project.Name, p.Hours, p.ProjectId))
            .ToList(),
        workDay.RateId);
}
