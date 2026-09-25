using System.Linq.Expressions;
using Moq;
using nInvoices.Application.DTOs;
using nInvoices.Application.Models;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.Services;

/// <summary>
/// The timesheet of an invoice that is not saved yet is rendered from the days being entered,
/// not from the ones stored for the month.
/// </summary>
[TestFixture]
public sealed class MonthlyReportPreviewTests
{
    private const long CustomerId = 1;

    private Mock<IRepository<MonthlyReportTemplate>> _templateRepository = null!;
    private Mock<IWorkDayRepository> _workDayRepository = null!;
    private MonthlyReportTemplateModel _capturedModel = null!;
    private MonthlyReportGenerationService _service = null!;
    private Customer _customer = null!;

    [SetUp]
    public void SetUp()
    {
        var template = new MonthlyReportTemplate(CustomerId, "Timesheet", "<html/>");
        template.Activate();

        _templateRepository = new Mock<IRepository<MonthlyReportTemplate>>();
        _templateRepository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<MonthlyReportTemplate, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<MonthlyReportTemplate, bool>> p, CancellationToken _) =>
                new[] { template }.Where(p.Compile()).ToList());

        _workDayRepository = new Mock<IWorkDayRepository>();

        var renderer = new Mock<ITemplateRenderer>();
        renderer
            .Setup(r => r.RenderAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, object model, CancellationToken _) =>
            {
                _capturedModel = (MonthlyReportTemplateModel)model;
                return "<html>timesheet</html>";
            });

        _customer = new Customer("Acme", "ACME123", new Address("Main", "1", "Town", "12345", "Country")) { Id = CustomerId };
        _service = new MonthlyReportGenerationService(_templateRepository.Object, _workDayRepository.Object, renderer.Object);
    }

    private static Invoice MonthlyInvoice()
    {
        var invoice = new Invoice(
            CustomerId,
            InvoiceNumber.Generate("INV-{NUMBER:000}", 1, new DateTime(2026, 1, 31), null),
            InvoiceType.Monthly,
            new DateOnly(2026, 1, 31),
            new Money(800m, "EUR"),
            "EUR");
        invoice.SetMonthlyInvoiceDetails(2026, 1, 2);
        return invoice;
    }

    [Test]
    public async Task PreviewReportHtmlAsync_WithEnteredDays_BuildsTheMonthFromThem()
    {
        WorkDayDto[] days =
        [
            new(new DateOnly(2026, 1, 5), DayType.Worked, 8m, null, [new WorkDayProjectDto("Alpha", 6m), new WorkDayProjectDto("Beta", 2m)]),
            new(new DateOnly(2026, 1, 6), DayType.Worked, 4m, "Half day", [new WorkDayProjectDto("Alpha", 4m)]),
            new(new DateOnly(2026, 1, 7), DayType.PublicHoliday)
        ];

        var html = await _service.PreviewReportHtmlAsync(MonthlyInvoice(), _customer, days, TestContext.CurrentContext.CancellationToken);

        html.ShouldBe("<html>timesheet</html>");
        _workDayRepository.Verify(
            r => r.GetByCustomerAndMonthAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _capturedModel.MonthDays.Count.ShouldBe(31);
        _capturedModel.WorkedDaysCount.ShouldBe(2);
        _capturedModel.PublicHolidayCount.ShouldBe(1);
        _capturedModel.MonthDays.Single(d => d.DayNumber == 6).Hours.ShouldBe(4m);
        _capturedModel.MonthDays.Single(d => d.DayNumber == 6).Notes.ShouldBe("Half day");

        var alpha = _capturedModel.ProjectSummary.Single(p => p.Name == "Alpha");
        alpha.TotalHours.ShouldBe(10m);
        alpha.WorkedDays.ShouldBe(2);
    }

    [Test]
    public async Task PreviewReportHtmlAsync_NoActiveTemplate_Throws()
    {
        _templateRepository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<MonthlyReportTemplate, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _service.PreviewReportHtmlAsync(MonthlyInvoice(), _customer, [], TestContext.CurrentContext.CancellationToken));
    }
}
