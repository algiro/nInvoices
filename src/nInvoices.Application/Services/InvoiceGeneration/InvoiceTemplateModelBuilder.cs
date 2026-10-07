using System.Globalization;
using nInvoices.Application.DTOs;
using nInvoices.Application.Models;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;

namespace nInvoices.Application.Services.InvoiceGeneration;

/// <summary>
/// The model an invoice template renders from (its properties become the template's variables):
/// customer, billed lines, project summary, taxes and totals. Pure: built from the invoice, its
/// rates and its work days, the same way for a new invoice and for one being re-rendered.
/// </summary>
public static class InvoiceTemplateModelBuilder
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public static InvoiceTemplateModel Build(
        Invoice invoice,
        Customer customer,
        DayRates rates,
        IEnumerable<WorkDayDto>? workDays)
    {
        var rate = rates.Default;

        // Calculate monthly-specific values upfront
        int? workedDays = null;
        int? monthNumber = null;
        string? monthDescription = null;
        decimal? monthlyRate = null;
        decimal? totalExpenses = null;

        if (invoice.Type == InvoiceType.Monthly && invoice.WorkedDays.HasValue)
        {
            workedDays = invoice.WorkedDays.Value;
            monthNumber = invoice.Month ?? DateTime.UtcNow.Month;
            monthlyRate = rate.Price.Amount;
            totalExpenses = invoice.TotalExpenses.Amount;

            if (invoice.Month.HasValue && invoice.Year.HasValue)
            {
                var date = new DateTime(invoice.Year.Value, invoice.Month.Value, 1);
                monthDescription = date.ToString("MMMM", English);
            }
        }

        return new InvoiceTemplateModel
        {
            InvoiceNumber = invoice.Number.ToString(),
            InvoiceType = invoice.Type.ToString(),
            Date = invoice.IssueDate.ToDateTime(TimeOnly.MinValue),
            DueDate = invoice.DueDate?.ToDateTime(TimeOnly.MinValue),
            Currency = rate.Price.Currency,
            Locale = customer.Locale,

            Customer = new CustomerTemplateModel
            {
                Name = customer.Name,
                FiscalId = customer.FiscalId,
                Address = new AddressTemplateModel
                {
                    Street = customer.Address.Street,
                    City = customer.Address.City,
                    PostalCode = customer.Address.ZipCode,  // ZipCode → PostalCode
                    Country = customer.Address.Country
                }
            },

            LineItems = BuildLineItems(invoice, rates, workDays, customer.Locale),
            ProjectSummary = BuildProjectSummary(workDays, rates),
            Taxes = invoice.TaxLines.Select(t => new TaxTemplateModel
            {
                Description = t.Description,
                Rate = t.Rate,
                Amount = t.TaxAmount.Amount  // TaxAmount, not Amount
            }).ToList(),

            Subtotal = invoice.Subtotal.Amount,
            TotalTax = invoice.TotalTaxes.Amount,
            Total = invoice.Total.Amount,

            // Monthly invoice specific data
            WorkedDays = workedDays,
            MonthNumber = monthNumber,
            MonthDescription = monthDescription,
            MonthlyRate = monthlyRate,
            TotalExpenses = totalExpenses,
            WorkedDayItems = invoice.Type == InvoiceType.Monthly && workDays != null
                ? workDays
                    .Where(wd => wd.DayType == DayType.Worked)
                    .OrderBy(wd => wd.Date)
                    .Select(wd => new WorkedDayTemplateModel
                    {
                        Date = wd.Date.ToString("d", CultureInfo.GetCultureInfo(customer.Locale)),
                        Hours = wd.HoursWorked ?? 8m
                    })
                    .ToList()
                : []
        };
    }

    /// <summary>
    /// Builds line items for the template.
    /// For Daily/Hourly rates with project allocations: one line item per project.
    /// For Daily/Hourly rates without allocations: one line item per worked day (date as description).
    /// Days billed at different rates get their own lines, each saying which rate it is.
    /// For Monthly rates: a single consolidated line.
    /// Expenses are added as separate line items.
    /// </summary>
    private static List<LineItemTemplateModel> BuildLineItems(
        Invoice invoice,
        DayRates rates,
        IEnumerable<WorkDayDto>? workDays,
        string locale)
    {
        var rate = rates.Default;
        var lineItems = new List<LineItemTemplateModel>();

        if (invoice.Type == InvoiceType.Monthly && invoice.WorkedDays.HasValue)
        {
            var workedDaysList = workDays?
                .Where(wd => wd.DayType == DayType.Worked)
                .OrderBy(wd => wd.Date)
                .ToList();

            var perDayRate = rate.Type is RateType.Daily or RateType.Hourly;

            if (workedDaysList is { Count: > 0 } && perDayRate)
            {
                // Days at the same rate are billed together, in the order the rates first appear
                var groups = workedDaysList.GroupBy(wd => rates.For(wd).Id).Select(g => g.ToList()).ToList();
                var mixed = groups.Count > 1;

                foreach (var days in groups)
                {
                    var groupRate = rates.For(days[0]);
                    var lines = BuildRateLineItems(days, groupRate, locale);

                    // With several rates on one invoice, say which rate each line is billed at
                    lineItems.AddRange(mixed
                        ? lines.Select(l => l with { Description = $"{l.Description} - {DayRates.Label(groupRate)}" })
                        : lines);
                }
            }
            else
            {
                // Monthly rate: single consolidated line item
                var description = $"Professional Services - {invoice.WorkedDays.Value} days";
                if (invoice.Month.HasValue && invoice.Year.HasValue)
                {
                    var date = new DateTime(invoice.Year.Value, invoice.Month.Value, 1);
                    description = $"Professional Services for {date.ToString("MMMM yyyy", English)}";
                }

                lineItems.Add(new LineItemTemplateModel
                {
                    Description = description,
                    Quantity = invoice.WorkedDays.Value,
                    Rate = rate.Price.Amount,
                    Amount = invoice.Subtotal.Amount
                });
            }
        }
        else if (invoice.Type == InvoiceType.OneTime)
        {
            // An hourly one-time invoice records its hours; any other is billed as one fixed amount
            var hourly = rate.Type == RateType.Hourly && invoice.Hours.HasValue;
            lineItems.Add(new LineItemTemplateModel
            {
                Description = "Professional Services",
                Quantity = hourly ? invoice.Hours!.Value : 1,
                Rate = rate.Price.Amount,
                Amount = invoice.Subtotal.Amount
            });
        }

        // Add expenses as separate line items
        foreach (var expense in invoice.Expenses)
        {
            lineItems.Add(new LineItemTemplateModel
            {
                Description = $"Expense: {expense.Description}",
                Quantity = 1,
                Rate = expense.Amount.Amount,
                Amount = expense.Amount.Amount
            });
        }

        return lineItems;
    }

    /// <summary>The lines for worked days billed at one daily or hourly rate: per project, or per day.</summary>
    private static List<LineItemTemplateModel> BuildRateLineItems(List<WorkDayDto> days, Rate rate, string locale)
    {
        var hasAllocations = days.Any(wd => wd.Projects is { Count: > 0 });
        if (hasAllocations)
        {
            // One line item per project (time grouped across the month)
            return BuildProjectLineItems(days, rate).ToList();
        }

        // One line item per worked day
        var culture = CultureInfo.GetCultureInfo(locale);
        return days
            .Select(wd =>
            {
                var quantity = rate.Type == RateType.Hourly ? InvoiceCalculator.DayHours(wd) : 1m;
                return new LineItemTemplateModel
                {
                    Description = wd.Date.ToString("d", culture),
                    Quantity = quantity,
                    Rate = rate.Price.Amount,
                    Amount = rate.Price.Amount * quantity
                };
            })
            .ToList();
    }

    /// <summary>
    /// One billed line per project. For hourly rates the quantity is total hours;
    /// for daily rates each day is split across its projects pro-rata by hours, so the
    /// quantities still sum to the worked-day count. Worked days with no allocation are
    /// grouped into a single "Unassigned" line.
    /// </summary>
    private static IEnumerable<LineItemTemplateModel> BuildProjectLineItems(
        List<WorkDayDto> workedDays,
        Rate rate)
    {
        var order = new List<string>();
        var byProject = new Dictionary<string, (decimal Hours, decimal Days, decimal Amount)>(StringComparer.OrdinalIgnoreCase);
        var unassignedDays = 0m;
        var unassignedHours = 0m;

        foreach (var wd in workedDays)
        {
            var allocations = wd.Projects?.Where(p => p.Hours > 0).ToList() ?? [];

            if (allocations.Count == 0)
            {
                unassignedDays += 1m;
                unassignedHours += InvoiceCalculator.DayHours(wd);
                continue;
            }

            var dayHours = allocations.Sum(a => a.Hours);

            foreach (var allocation in allocations)
            {
                var name = allocation.ProjectName.Trim();
                if (!byProject.TryGetValue(name, out var acc))
                {
                    order.Add(name);
                    acc = (0m, 0m, 0m);
                }

                var dayFraction = dayHours > 0 ? allocation.Hours / dayHours : 0m;
                var amount = rate.Type == RateType.Hourly
                    ? rate.Price.Amount * allocation.Hours
                    : rate.Price.Amount * dayFraction;

                byProject[name] = (
                    acc.Hours + allocation.Hours,
                    acc.Days + dayFraction,
                    acc.Amount + amount);
            }
        }

        foreach (var name in order)
        {
            var acc = byProject[name];
            yield return new LineItemTemplateModel
            {
                Description = name,
                Quantity = rate.Type == RateType.Hourly ? acc.Hours : decimal.Round(acc.Days, 3),
                Rate = rate.Price.Amount,
                Amount = acc.Amount
            };
        }

        if (unassignedDays > 0m || unassignedHours > 0m)
        {
            yield return new LineItemTemplateModel
            {
                Description = "Unassigned",
                Quantity = rate.Type == RateType.Hourly ? unassignedHours : unassignedDays,
                Rate = rate.Price.Amount,
                Amount = rate.Type == RateType.Hourly
                    ? rate.Price.Amount * unassignedHours
                    : rate.Price.Amount * unassignedDays
            };
        }
    }

    /// <summary>
    /// Per-project totals for the billed month. <see cref="ProjectSummaryTemplateModel.Amount"/>
    /// is null for a flat monthly rate (time is not billed per project).
    /// </summary>
    private static List<ProjectSummaryTemplateModel> BuildProjectSummary(
        IEnumerable<WorkDayDto>? workDays,
        DayRates rates)
    {
        if (workDays is null)
            return [];

        var order = new List<string>();
        var byProject = new Dictionary<string, (decimal Hours, HashSet<DateOnly> Days, decimal Amount)>(StringComparer.OrdinalIgnoreCase);

        foreach (var wd in workDays.Where(wd => wd.DayType == DayType.Worked))
        {
            var allocations = wd.Projects?.Where(p => p.Hours > 0).ToList() ?? [];
            if (allocations.Count == 0)
                continue;

            var dayHours = allocations.Sum(a => a.Hours);
            var rate = rates.For(wd);

            foreach (var allocation in allocations)
            {
                var name = allocation.ProjectName.Trim();
                if (!byProject.TryGetValue(name, out var acc))
                {
                    order.Add(name);
                    acc = (0m, new HashSet<DateOnly>(), 0m);
                }

                var amount = rate.Type switch
                {
                    RateType.Hourly => rate.Price.Amount * allocation.Hours,
                    RateType.Daily => dayHours > 0 ? rate.Price.Amount * (allocation.Hours / dayHours) : 0m,
                    _ => 0m
                };

                acc.Days.Add(wd.Date);
                byProject[name] = (acc.Hours + allocation.Hours, acc.Days, acc.Amount + amount);
            }
        }

        return order
            .Select(name =>
            {
                var acc = byProject[name];
                return new ProjectSummaryTemplateModel
                {
                    Name = name,
                    TotalHours = acc.Hours,
                    WorkedDays = acc.Days.Count,
                    Amount = rates.IsFixedMonthly ? null : acc.Amount
                };
            })
            .ToList();
    }
}
