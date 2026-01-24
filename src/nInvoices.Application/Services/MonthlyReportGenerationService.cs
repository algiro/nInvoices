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
    private readonly IRepository<WorkDay> _workDayRepository;
    private readonly ITemplateRenderer _templateRenderer;

    public MonthlyReportGenerationService(
        IRepository<MonthlyReportTemplate> templateRepository,
        IRepository<WorkDay> workDayRepository,
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

        // Get active template
        var templates = await _templateRepository.FindAsync(
            t => t.CustomerId == customer.Id && t.InvoiceType == InvoiceType.Monthly && t.IsActive,
            cancellationToken);

        var template = templates.FirstOrDefault()
            ?? throw new InvalidOperationException($"No active monthly report template found for customer {customer.Id}");

        // Get work days for this month
        var year = invoice.Year.Value;
        var month = invoice.Month.Value;
        var startDate = new DateOnly(year, month, 1);
        var endDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        var workDays = await _workDayRepository.FindAsync(
            wd => wd.CustomerId == customer.Id && wd.Date >= startDate && wd.Date <= endDate,
            cancellationToken);

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
            
            var dayModel = new MonthDayTemplateModel
            {
                Date = date.ToString("dd/MM/yyyy"),
                DayOfWeek = dateTime.ToString("dddd", System.Globalization.CultureInfo.GetCultureInfo("en-US")),
                DayNumber = day,
                Type = workedDay != null ? workedDay.DayType.ToString() : string.Empty,
                IsWeekend = isWeekend,
                IsWorked = workedDay?.DayType == DayType.Worked,
                IsPublicHoliday = workedDay?.DayType == DayType.PublicHoliday,
                IsUnpaidLeave = workedDay?.DayType == DayType.UnpaidLeave,
                Notes = workedDay?.Notes
            };

            monthDays.Add(dayModel);
        }

        // Calculate summary
        var workedDaysCount = workDays.Count(wd => wd.DayType == DayType.Worked);
        var publicHolidayCount = workDays.Count(wd => wd.DayType == DayType.PublicHoliday);
        var unpaidLeaveCount = workDays.Count(wd => wd.DayType == DayType.UnpaidLeave);

        var monthDate = new DateTime(year, month, 1);
        var monthDescription = monthDate.ToString("MMMM", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

        // Build model
        var model = new MonthlyReportTemplateModel
        {
            CustomerName = customer.Name,
            MonthDescription = monthDescription,
            Year = year,
            MonthNumber = month,
            MonthDays = monthDays,
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
}
