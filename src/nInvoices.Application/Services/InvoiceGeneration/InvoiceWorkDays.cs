using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Services.InvoiceGeneration;

/// <summary>The work days a monthly invoice bills, as they are saved for its customer and month.</summary>
public interface IInvoiceWorkDays
{
    /// <summary>The saved work days of the month, with their project allocations.</summary>
    Task<List<WorkDayDto>> LoadMonthAsync(long customerId, int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the month's work days (staged; the caller saves), resolving each project allocation
    /// and creating projects typed on the calendar for the first time.
    /// </summary>
    /// <returns>The days with project names in their canonical form and day hours set to the sum of their allocations.</returns>
    Task<ICollection<WorkDayDto>> ReplaceMonthAsync(
        long customerId,
        int year,
        int month,
        ICollection<WorkDayDto> workDays,
        CancellationToken cancellationToken = default);
}

public sealed class InvoiceWorkDays : IInvoiceWorkDays
{
    private readonly IWorkDayRepository _workDayRepository;
    private readonly IProjectResolver _projectResolver;

    public InvoiceWorkDays(IWorkDayRepository workDayRepository, IProjectResolver projectResolver)
    {
        _workDayRepository = workDayRepository;
        _projectResolver = projectResolver;
    }

    public async Task<List<WorkDayDto>> LoadMonthAsync(long customerId, int year, int month, CancellationToken cancellationToken = default)
    {
        var saved = await _workDayRepository.GetByCustomerAndMonthAsync(customerId, year, month, cancellationToken);
        return saved.Select(WorkDayMapper.ToDto).ToList();
    }

    public async Task<ICollection<WorkDayDto>> ReplaceMonthAsync(
        long customerId,
        int year,
        int month,
        ICollection<WorkDayDto> workDays,
        CancellationToken cancellationToken = default)
    {
        if (workDays.Count == 0)
            return workDays;

        // Delete all work days for this customer in this month
        var startDate = new DateOnly(year, month, 1);
        var endDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        var existingWorkDays = await _workDayRepository.FindAsync(
            wd => wd.CustomerId == customerId && wd.Date >= startDate && wd.Date <= endDate,
            cancellationToken);

        foreach (var existing in existingWorkDays)
            await _workDayRepository.DeleteAsync(existing, cancellationToken);

        var normalized = new List<WorkDayDto>(workDays.Count);

        foreach (var dto in workDays)
        {
            var workDay = new WorkDay(customerId, dto.Date, dto.DayType, dto.HoursWorked, dto.Notes)
            {
                RateId = dto.DayType == DayType.Worked ? dto.RateId : null
            };
            List<WorkDayProjectDto>? normalizedAllocations = null;

            if (dto.Projects is { Count: > 0 })
            {
                normalizedAllocations = [];
                var totalHours = 0m;

                // Merge duplicate references to the same project within a single day
                var groups = dto.Projects
                    .Where(p => p.Hours > 0)
                    .GroupBy(p => new { p.ProjectId, Name = (p.ProjectName ?? string.Empty).Trim() });

                foreach (var group in groups)
                {
                    var hours = group.Sum(p => p.Hours);
                    var project = await _projectResolver.ResolveOrCreateAsync(
                        customerId, group.Key.ProjectId, group.Key.Name, cancellationToken);

                    workDay.Projects.Add(new WorkDayProject(project, hours));
                    normalizedAllocations.Add(new WorkDayProjectDto(
                        project.Name, hours, project.Id > 0 ? project.Id : null));
                    totalHours += hours;
                }

                if (totalHours > 0m)
                    workDay.HoursWorked = totalHours;
            }

            await _workDayRepository.AddAsync(workDay, cancellationToken);
            normalized.Add(dto with { Projects = normalizedAllocations });
        }

        return normalized;
    }
}
