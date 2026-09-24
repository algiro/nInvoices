using nInvoices.Application.Models;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Services;

/// <summary>
/// Result of rendering a template against sample data.
/// </summary>
/// <param name="Html">The rendered HTML, or null when the template could not be rendered.</param>
/// <param name="Errors">Syntax or rendering errors, in a form the template author can act on.</param>
public sealed record TemplatePreviewResult(string? Html, IReadOnlyList<string> Errors);

/// <summary>
/// Renders invoice and monthly report templates with realistic sample data so they can be
/// previewed while editing, without creating an invoice.
/// </summary>
public interface ITemplatePreviewService
{
    Task<TemplatePreviewResult> PreviewInvoiceAsync(string content, long? customerId, CancellationToken cancellationToken = default);
    Task<TemplatePreviewResult> PreviewMonthlyReportAsync(string content, long? customerId, CancellationToken cancellationToken = default);
}

public sealed class TemplatePreviewService : ITemplatePreviewService
{
    private const decimal FullDayHours = 8m;

    private readonly ITemplateRenderer _renderer;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Rate> _rateRepository;
    private readonly IRepository<Tax> _taxRepository;

    public TemplatePreviewService(
        ITemplateRenderer renderer,
        IRepository<Customer> customerRepository,
        IRepository<Rate> rateRepository,
        IRepository<Tax> taxRepository)
    {
        _renderer = renderer;
        _customerRepository = customerRepository;
        _rateRepository = rateRepository;
        _taxRepository = taxRepository;
    }

    public async Task<TemplatePreviewResult> PreviewInvoiceAsync(string content, long? customerId, CancellationToken cancellationToken = default)
    {
        var sample = await LoadSampleContextAsync(customerId, cancellationToken);
        return await RenderAsync(content, BuildInvoiceModel(sample), cancellationToken);
    }

    public async Task<TemplatePreviewResult> PreviewMonthlyReportAsync(string content, long? customerId, CancellationToken cancellationToken = default)
    {
        var sample = await LoadSampleContextAsync(customerId, cancellationToken);
        return await RenderAsync(content, BuildMonthlyReportModel(sample), cancellationToken);
    }

    private async Task<TemplatePreviewResult> RenderAsync(string content, object model, CancellationToken cancellationToken)
    {
        var validation = await _renderer.ValidateAsync(content, cancellationToken);
        if (!validation.IsValid)
            return new TemplatePreviewResult(null, validation.Errors);

        try
        {
            var html = await _renderer.RenderAsync(content, model, cancellationToken);
            return new TemplatePreviewResult(html, []);
        }
        catch (InvalidOperationException ex)
        {
            // The renderer wraps Scriban's runtime error (e.g. an unknown function or a bad argument)
            var reason = ex.InnerException?.Message ?? ex.Message;
            return new TemplatePreviewResult(null, [reason]);
        }
    }

    // ---------- sample data ----------

    private sealed record SampleContext(
        CustomerTemplateModel Customer,
        string Locale,
        decimal DailyRate,
        string Currency,
        IReadOnlyList<(string Description, decimal Rate)> Taxes,
        DateTime Month);

    private async Task<SampleContext> LoadSampleContextAsync(long? customerId, CancellationToken cancellationToken)
    {
        var customer = customerId is > 0
            ? await _customerRepository.GetByIdAsync(customerId.Value, cancellationToken)
            : null;

        var customerModel = customer is null
            ? new CustomerTemplateModel
            {
                Name = "Northwind Capital S.p.A.",
                FiscalId = "IT01234567890",
                Address = new AddressTemplateModel { Street = "Via Roma 10", City = "Milano", PostalCode = "20121", Country = "Italy" }
            }
            : new CustomerTemplateModel
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
            };

        var dailyRate = 500m;
        var currency = "EUR";
        var taxes = new List<(string, decimal)> { ("VAT", 22m) };

        if (customer is not null)
        {
            var rates = await _rateRepository.FindAsync(r => r.CustomerId == customer.Id, cancellationToken);
            var rate = rates.FirstOrDefault(r => r.Type == RateType.Daily) ?? rates.FirstOrDefault();
            if (rate is not null)
            {
                dailyRate = rate.Type switch
                {
                    RateType.Hourly => rate.Price.Amount * FullDayHours,
                    RateType.Monthly => Math.Round(rate.Price.Amount / 20m, 2),
                    _ => rate.Price.Amount
                };
                currency = rate.Price.Currency;
            }

            var customerTaxes = await _taxRepository.FindAsync(t => t.CustomerId == customer.Id && t.IsActive, cancellationToken);
            if (customerTaxes.Any())
                taxes = customerTaxes.OrderBy(t => t.Order).Select(t => (t.Description, t.Rate)).ToList();
        }

        // Preview the month before the current one, the usual invoicing period
        var today = DateTime.Today;
        var month = new DateTime(today.Year, today.Month, 1).AddMonths(-1);

        return new SampleContext(customerModel, customer?.Locale ?? "en-US", dailyRate, currency, taxes, month);
    }

    /// <summary>
    /// A month of weekdays worked on two projects, with one half day, one public holiday and
    /// one unpaid leave day, so every branch of a typical template shows up in the preview.
    /// </summary>
    private static List<MonthDayTemplateModel> BuildSampleDays(DateTime month)
    {
        var days = new List<MonthDayTemplateModel>();
        var weekdayIndex = 0;
        for (var date = month; date.Month == month.Month; date = date.AddDays(1))
        {
            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (isWeekend)
            {
                days.Add(new MonthDayTemplateModel { DateValue = date, DayNumber = date.Day, IsWeekend = true });
                continue;
            }

            weekdayIndex++;
            var day = weekdayIndex switch
            {
                4 => Holiday(date),
                13 => Leave(date),
                7 => Worked(date, [("Trade reporting gateway", 4m)], "Half day, medical appointment"),
                10 => Worked(date, [("Trade reporting gateway", 5m), ("FIX certification", 3m)], "Certification session with the exchange"),
                _ => Worked(date, [(weekdayIndex % 5 == 0 ? "FIX certification" : "Trade reporting gateway", FullDayHours)], null)
            };
            days.Add(day);
        }
        return days;

        static MonthDayTemplateModel Holiday(DateTime d) => new()
        {
            DateValue = d, DayNumber = d.Day, Type = nameof(DayType.PublicHoliday), IsPublicHoliday = true, Notes = "Public holiday"
        };

        static MonthDayTemplateModel Leave(DateTime d) => new()
        {
            DateValue = d, DayNumber = d.Day, Type = nameof(DayType.UnpaidLeave), IsUnpaidLeave = true
        };

        static MonthDayTemplateModel Worked(DateTime d, (string Name, decimal Hours)[] projects, string? note) => new()
        {
            DateValue = d,
            DayNumber = d.Day,
            Type = nameof(DayType.Worked),
            IsWorked = true,
            Hours = projects.Sum(p => p.Hours),
            Notes = note,
            Projects = projects.Select(p => new DayProjectTemplateModel { Name = p.Name, Hours = p.Hours }).ToList()
        };
    }

    private static List<ProjectSummaryTemplateModel> Summarize(List<MonthDayTemplateModel> days, decimal dailyRate) =>
        days.SelectMany(d => d.Projects.Select(p => (Day: d, Project: p)))
            .GroupBy(x => x.Project.Name)
            .Select(g => new ProjectSummaryTemplateModel
            {
                Name = g.Key,
                TotalHours = g.Sum(x => x.Project.Hours),
                WorkedDays = g.Select(x => x.Day.DayNumber).Distinct().Count(),
                Amount = Math.Round(g.Sum(x => x.Project.Hours) / FullDayHours * dailyRate, 2)
            })
            .OrderBy(p => p.Name)
            .ToList();

    private static InvoiceTemplateModel BuildInvoiceModel(SampleContext sample)
    {
        var days = BuildSampleDays(sample.Month);
        var worked = days.Where(d => d.IsWorked).ToList();
        var projects = Summarize(days, sample.DailyRate);

        var lineItems = projects
            .Select(p => new LineItemTemplateModel
            {
                Description = p.Name,
                Quantity = Math.Round(p.TotalHours / FullDayHours, 2),
                Rate = sample.DailyRate,
                Amount = p.Amount ?? 0m
            })
            .ToList();

        var subtotal = lineItems.Sum(l => l.Amount);
        var taxes = sample.Taxes
            .Select(t => new TaxTemplateModel { Description = t.Description, Rate = t.Rate, Amount = Math.Round(subtotal * t.Rate / 100m, 2) })
            .ToList();
        var totalTax = taxes.Sum(t => t.Amount);
        var culture = CultureOrInvariant(sample.Locale);

        return new InvoiceTemplateModel
        {
            InvoiceNumber = $"{sample.Month:yy-MM}-001",
            InvoiceType = nameof(InvoiceType.Monthly),
            Date = sample.Month.AddMonths(1).AddDays(-1),
            DueDate = sample.Month.AddMonths(2).AddDays(-1),
            Currency = sample.Currency,
            Customer = sample.Customer,
            LineItems = lineItems,
            Taxes = taxes,
            ProjectSummary = projects,
            Subtotal = subtotal,
            TotalTax = totalTax,
            Total = subtotal + totalTax,
            WorkedDays = worked.Count,
            MonthNumber = sample.Month.Month,
            MonthDescription = sample.Month.ToString("MMMM yyyy", culture),
            MonthlyRate = sample.DailyRate,
            TotalExpenses = 0m,
            WorkedDayItems = worked
                .Select(d => new WorkedDayTemplateModel { Date = d.DateValue.ToString("d", culture), Hours = d.Hours ?? FullDayHours })
                .ToList()
        };
    }

    private static System.Globalization.CultureInfo CultureOrInvariant(string locale)
    {
        try
        {
            return System.Globalization.CultureInfo.GetCultureInfo(locale);
        }
        catch (System.Globalization.CultureNotFoundException)
        {
            return System.Globalization.CultureInfo.InvariantCulture;
        }
    }

    private static MonthlyReportTemplateModel BuildMonthlyReportModel(SampleContext sample)
    {
        var days = BuildSampleDays(sample.Month);
        var worked = days.Where(d => d.IsWorked).ToList();
        var effectiveDays = worked.Sum(d => (d.Hours ?? FullDayHours) / FullDayHours);

        return new MonthlyReportTemplateModel
        {
            CustomerName = sample.Customer.Name,
            Customer = sample.Customer,
            Locale = sample.Locale,
            Year = sample.Month.Year,
            MonthNumber = sample.Month.Month,
            MonthDays = days,
            ProjectSummary = Summarize(days, sample.DailyRate),
            WorkedDaysCount = worked.Count,
            PublicHolidayCount = days.Count(d => d.IsPublicHoliday),
            UnpaidLeaveCount = days.Count(d => d.IsUnpaidLeave),
            TotalDaysInMonth = days.Count,
            DailyRate = sample.DailyRate,
            Currency = sample.Currency,
            TotalAmount = Math.Round(effectiveDays * sample.DailyRate, 2)
        };
    }
}
