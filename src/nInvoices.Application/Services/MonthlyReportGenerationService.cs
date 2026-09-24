using nInvoices.Application.DTOs;
using nInvoices.Application.Models;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Services;

/// <summary>
/// Service for generating monthly work reports.
/// Builds report models with all month days and their types.
/// </summary>
public interface IMonthlyReportGenerationService
{
    Task<string> GenerateReportHtmlAsync(
        Invoice invoice,
        Customer customer,
        CancellationToken cancellationToken = default);
}

public sealed class MonthlyReportGenerationService : IMonthlyReportGenerationService
{
    private readonly IRepository<MonthlyReportTemplate> _templateRepository;
    private readonly IWorkDayRepository _workDayRepository;
    private readonly ITemplateRenderer _templateRenderer;

    public MonthlyReportGenerationService(
        IRepository<MonthlyReportTemplate> templateRepository,
        IWorkDayRepository workDayRepository,
        ITemplateRenderer templateRenderer)
    {
        _templateRepository = templateRepository;
        _workDayRepository = workDayRepository;
        _templateRenderer = templateRenderer;
    }

    public async Task<string> GenerateReportHtmlAsync(
        Invoice invoice,
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(customer);

        if (invoice.Type != InvoiceType.Monthly)
            throw new InvalidOperationException("Monthly reports can only be generated for monthly invoices");

        if (!invoice.Month.HasValue || !invoice.Year.HasValue)
            throw new InvalidOperationException("Invoice must have month and year set");

        MonthlyReportTemplate template;

        // Use the template ID from the invoice if specified, otherwise use active template
        if (invoice.MonthlyReportTemplateId.HasValue)
        {
            template = await _templateRepository.GetByIdAsync(invoice.MonthlyReportTemplateId.Value, cancellationToken)
                ?? throw new InvalidOperationException($"Monthly report template {invoice.MonthlyReportTemplateId.Value} not found");
        }
        else
        {
            // Get active template (backward compatibility)
            var templates = await _templateRepository.FindAsync(
                t => t.CustomerId == customer.Id && t.InvoiceType == InvoiceType.Monthly && t.IsActive,
                cancellationToken);

            template = templates.FirstOrDefault()
                ?? throw new InvalidOperationException($"No active monthly report template found for customer {customer.Id}");
        }

        // Get work days for this month (with project allocations eagerly loaded)
        var year = invoice.Year.Value;
        var month = invoice.Month.Value;

        var workDays = await _workDayRepository.GetByCustomerAndMonthAsync(
            customer.Id, year, month, cancellationToken);

        // Build model
        var model = BuildMonthlyReportModel(invoice, customer, workDays.ToList());

        // Render HTML
        var html = await _templateRenderer.RenderAsync(template.Content, model, cancellationToken);

        return html;
    }

    private MonthlyReportTemplateModel BuildMonthlyReportModel(Invoice invoice, Customer customer, List<WorkDay> workDays)
    {
        var year = invoice.Year!.Value;
        var month = invoice.Month!.Value;
        var daysInMonth = DateTime.DaysInMonth(year, month);

        // Index work days by date
        var workDaysDict = workDays.ToDictionary(wd => wd.Date, wd => wd);

        // Build day models for all days in the month
        var monthDays = new List<MonthDayTemplateModel>();
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            var dateTime = date.ToDateTime(TimeOnly.MinValue);
            var isWeekend = dateTime.DayOfWeek == DayOfWeek.Saturday || dateTime.DayOfWeek == DayOfWeek.Sunday;

            WorkDay? workedDay = workDaysDict.GetValueOrDefault(date);

            var dayAllocations = workedDay?.Projects
                .Where(p => p.Hours > 0)
                .OrderBy(p => p.Project.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => new DayProjectTemplateModel { Name = p.Project.Name, Hours = p.Hours })
                .ToList() ?? [];

            decimal? dayHours = dayAllocations.Count > 0
                ? dayAllocations.Sum(p => p.Hours)
                : workedDay?.HoursWorked;

            var dayModel = new MonthDayTemplateModel
            {
                DateValue = dateTime,
                DayNumber = day,
                Type = workedDay != null ? workedDay.DayType.ToString() : string.Empty,
                IsWeekend = isWeekend,
                IsWorked = workedDay?.DayType == DayType.Worked,
                IsPublicHoliday = workedDay?.DayType == DayType.PublicHoliday,
                IsUnpaidLeave = workedDay?.DayType == DayType.UnpaidLeave,
                Hours = workedDay?.DayType == DayType.Worked ? (dayHours ?? 8m) : null,
                Notes = workedDay?.Notes,
                Projects = dayAllocations
            };

            monthDays.Add(dayModel);
        }

        // Calculate summary
        var workedDaysCount = workDays.Count(wd => wd.DayType == DayType.Worked);
        var publicHolidayCount = workDays.Count(wd => wd.DayType == DayType.PublicHoliday);
        var unpaidLeaveCount = workDays.Count(wd => wd.DayType == DayType.UnpaidLeave);
        var projectSummary = BuildProjectSummary(workDays, invoice.Subtotal.Amount);

        // Build model
        var model = new MonthlyReportTemplateModel
        {
            CustomerName = customer.Name,
            Customer = new CustomerTemplateModel
            {
                Name = customer.Name,
                FiscalId = customer.FiscalId ?? string.Empty,
                Address = new AddressTemplateModel
                {
                    Street = customer.Address?.Street ?? string.Empty,
                    City = customer.Address?.City ?? string.Empty,
                    PostalCode = customer.Address?.ZipCode ?? string.Empty,
                    Country = customer.Address?.Country ?? string.Empty
                }
            },
            Locale = "it-IT", // Default locale - can be made configurable per customer/template in future
            Year = year,
            MonthNumber = month,
            MonthDays = monthDays,
            ProjectSummary = projectSummary,
            WorkedDaysCount = workedDaysCount,
            PublicHolidayCount = publicHolidayCount,
            UnpaidLeaveCount = unpaidLeaveCount,
            TotalDaysInMonth = daysInMonth,
            DailyRate = invoice.Subtotal.Amount / Math.Max(workedDaysCount, 1), // Avoid division by zero
            Currency = invoice.Subtotal.Currency,
            TotalAmount = invoice.Total.Amount
        };

        return model;
    }

    /// <summary>
    /// Per-project totals for the month. The invoice subtotal is allocated across projects
    /// pro-rata by tracked hours; <see cref="ProjectSummaryTemplateModel.Amount"/> is null
    /// when no hours were tracked for any project.
    /// </summary>
    private static List<ProjectSummaryTemplateModel> BuildProjectSummary(
        IReadOnlyList<WorkDay> workDays,
        decimal subtotal)
    {
        var order = new List<string>();
        var byProject = new Dictionary<string, (decimal Hours, HashSet<DateOnly> Days)>(StringComparer.OrdinalIgnoreCase);

        foreach (var wd in workDays.Where(wd => wd.DayType == DayType.Worked))
        {
            foreach (var allocation in wd.Projects.Where(p => p.Hours > 0))
            {
                var name = allocation.Project.Name;
                if (!byProject.TryGetValue(name, out var acc))
                {
                    order.Add(name);
                    acc = (0m, new HashSet<DateOnly>());
                }

                acc.Days.Add(wd.Date);
                byProject[name] = (acc.Hours + allocation.Hours, acc.Days);
            }
        }

        var totalHours = byProject.Values.Sum(v => v.Hours);

        return order
            .Select(name =>
            {
                var acc = byProject[name];
                return new ProjectSummaryTemplateModel
                {
                    Name = name,
                    TotalHours = acc.Hours,
                    WorkedDays = acc.Days.Count,
                    Amount = totalHours > 0m ? subtotal * (acc.Hours / totalHours) : null
                };
            })
            .ToList();
    }
}
