using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using nInvoices.Application.DTOs;
using nInvoices.Application.Models;
using nInvoices.Application.Services;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.Services;

/// <summary>
/// Covers the per-project work-day behaviour added to invoice generation:
/// project allocations become billed line items, feed the project summary, and
/// are persisted alongside the work day.
/// </summary>
[TestFixture]
public sealed class InvoiceGenerationServiceProjectTests
{
    private const long CustomerId = 1;
    private const string Currency = "EUR";

    private Mock<IRepository<InvoiceTemplate>> _templateRepository = null!;
    private Mock<IRepository<Customer>> _customerRepository = null!;
    private Mock<IRepository<Rate>> _rateRepository = null!;
    private Mock<IRepository<Tax>> _taxRepository = null!;
    private Mock<IInvoiceRepository> _invoiceRepository = null!;
    private Mock<IWorkDayRepository> _workDayRepository = null!;
    private Mock<IRepository<InvoiceSequence>> _sequenceRepository = null!;
    private Mock<IProjectResolver> _projectResolver = null!;
    private Mock<ITemplateRenderer> _templateRenderer = null!;
    private Mock<IHtmlToPdfConverter> _htmlToPdfConverter = null!;
    private Mock<ITaxCalculationService> _taxCalculationService = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;

    private List<Rate> _rates = null!;
    private List<WorkDay> _savedWorkDays = null!;
    private Dictionary<string, Project> _projectsByName = null!;
    private long _nextProjectId;
    private InvoiceTemplateModel _capturedModel = null!;

    private InvoiceGenerationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _rates = [];
        _savedWorkDays = [];
        _projectsByName = new Dictionary<string, Project>(StringComparer.OrdinalIgnoreCase);
        _nextProjectId = 100;

        _templateRepository = new Mock<IRepository<InvoiceTemplate>>();
        _customerRepository = new Mock<IRepository<Customer>>();
        _rateRepository = new Mock<IRepository<Rate>>();
        _taxRepository = new Mock<IRepository<Tax>>();
        _invoiceRepository = new Mock<IInvoiceRepository>();
        _workDayRepository = new Mock<IWorkDayRepository>();
        _sequenceRepository = new Mock<IRepository<InvoiceSequence>>();
        _projectResolver = new Mock<IProjectResolver>();
        _templateRenderer = new Mock<ITemplateRenderer>();
        _htmlToPdfConverter = new Mock<IHtmlToPdfConverter>();
        _taxCalculationService = new Mock<ITaxCalculationService>();
        _unitOfWork = new Mock<IUnitOfWork>();

        var template = new InvoiceTemplate(CustomerId, InvoiceType.Monthly, "T", "<html/>");
        template.Activate();
        _templateRepository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<InvoiceTemplate, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<InvoiceTemplate, bool>> p, CancellationToken _) =>
                new[] { template }.Where(p.Compile()).ToList());

        _rateRepository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Rate, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Rate, bool>> p, CancellationToken _) => _rates.Where(p.Compile()).ToList());

        _taxRepository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Tax, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var customer = new Customer("Acme", "ACME123", new Address("Main", "1", "Town", "12345", "Country"))
        {
            Id = CustomerId
        };
        _customerRepository
            .Setup(r => r.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        _sequenceRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceSequence(1));

        _workDayRepository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<WorkDay, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _workDayRepository
            .Setup(r => r.AddAsync(It.IsAny<WorkDay>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkDay wd, CancellationToken _) => { _savedWorkDays.Add(wd); return wd; });

        _projectResolver
            .Setup(r => r.ResolveOrCreateAsync(CustomerId, It.IsAny<long?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long cid, long? _, string name, CancellationToken _) =>
            {
                var trimmed = name.Trim();
                if (!_projectsByName.TryGetValue(trimmed, out var project))
                {
                    project = new Project(cid, trimmed) { Id = _nextProjectId++ };
                    _projectsByName[trimmed] = project;
                }
                return project;
            });

        _taxCalculationService
            .Setup(s => s.CalculateTaxes(It.IsAny<IEnumerable<Tax>>(), It.IsAny<Money>()))
            .Returns((Money.Zero(Currency), Enumerable.Empty<InvoiceTaxLine>()));

        _templateRenderer
            .Setup(r => r.RenderAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, object model, CancellationToken _) =>
            {
                _capturedModel = (InvoiceTemplateModel)model;
                return "<html>rendered</html>";
            });

        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var settings = Options.Create(new InvoiceSettings { NumberFormat = "INV-{YEAR}-{NUMBER:000}" });

        _service = new InvoiceGenerationService(
            _templateRepository.Object,
            _customerRepository.Object,
            _rateRepository.Object,
            _taxRepository.Object,
            _invoiceRepository.Object,
            _workDayRepository.Object,
            _sequenceRepository.Object,
            _projectResolver.Object,
            _templateRenderer.Object,
            _htmlToPdfConverter.Object,
            _taxCalculationService.Object,
            settings,
            _unitOfWork.Object);
    }

    private void GivenRate(RateType type, decimal amount)
    {
        _rates.Add(new Rate(CustomerId, type, new Money(amount, Currency)) { Id = _rates.Count + 1 });
    }

    private static WorkDayDto WorkedDay(int day, params (string name, decimal hours)[] projects) =>
        new(
            new DateOnly(2026, 1, day),
            DayType.Worked,
            Projects: projects.Select(p => new WorkDayProjectDto(p.name, p.hours)).ToList());

    private GenerateInvoiceDto MonthlyDto(params WorkDayDto[] workDays) => new()
    {
        CustomerId = CustomerId,
        InvoiceType = InvoiceType.Monthly,
        Year = 2026,
        Month = 1,
        WorkDays = workDays
    };

    [Test]
    public async Task GenerateInvoiceAsync_DailyRateWithProjects_ProducesOneLinePerProjectWithFractionalDays()
    {
        GivenRate(RateType.Daily, 400m);

        // Day 1: 8h split 6/2 across Alpha/Beta.  Day 2: all Alpha.
        var dto = MonthlyDto(
            WorkedDay(1, ("Alpha", 6m), ("Beta", 2m)),
            WorkedDay(2, ("Alpha", 8m)));

        var invoice = await _service.GenerateInvoiceAsync(dto, TestContext.CurrentContext.CancellationToken);

        invoice.Subtotal.Amount.ShouldBe(800m); // 2 worked days x 400

        var projectLines = _capturedModel.LineItems.Where(l => l.Description is "Alpha" or "Beta").ToList();
        projectLines.Select(l => l.Description).ShouldBe(["Alpha", "Beta"]);
        // Alpha = day1 (6/8) + day2 (1.0) = 1.75 days ; Beta = day1 (2/8) = 0.25 days
        projectLines.Single(l => l.Description == "Alpha").Quantity.ShouldBe(1.75m);
        projectLines.Single(l => l.Description == "Beta").Quantity.ShouldBe(0.25m);
        projectLines.Sum(l => l.Amount).ShouldBe(800m);
    }

    [Test]
    public async Task GenerateInvoiceAsync_HourlyRateWithProjects_BillsTotalHoursPerProject()
    {
        GivenRate(RateType.Hourly, 50m);

        var dto = MonthlyDto(
            WorkedDay(1, ("Alpha", 3m), ("Beta", 5m)),
            WorkedDay(2, ("Alpha", 4m)));

        var invoice = await _service.GenerateInvoiceAsync(dto, TestContext.CurrentContext.CancellationToken);

        invoice.Subtotal.Amount.ShouldBe(600m); // (3+5+4) hours x 50

        var alpha = _capturedModel.LineItems.Single(l => l.Description == "Alpha");
        alpha.Quantity.ShouldBe(7m);
        alpha.Amount.ShouldBe(350m);
        _capturedModel.LineItems.Single(l => l.Description == "Beta").Amount.ShouldBe(250m);
    }

    [Test]
    public async Task GenerateInvoiceAsync_WithProjects_PopulatesProjectSummary()
    {
        GivenRate(RateType.Hourly, 50m);

        var dto = MonthlyDto(
            WorkedDay(1, ("Alpha", 3m), ("Beta", 5m)),
            WorkedDay(2, ("Alpha", 4m)));

        await _service.GenerateInvoiceAsync(dto, TestContext.CurrentContext.CancellationToken);

        var alpha = _capturedModel.ProjectSummary.Single(p => p.Name == "Alpha");
        alpha.TotalHours.ShouldBe(7m);
        alpha.WorkedDays.ShouldBe(2);
        alpha.Amount.ShouldBe(350m);
    }

    [Test]
    public async Task GenerateInvoiceAsync_PersistsWorkDayProjectsAndDerivesHours()
    {
        GivenRate(RateType.Hourly, 50m);

        var dto = MonthlyDto(WorkedDay(1, ("Alpha", 3m), ("Beta", 5m)));

        await _service.GenerateInvoiceAsync(dto, TestContext.CurrentContext.CancellationToken);

        var saved = _savedWorkDays.Single();
        saved.Projects.Count.ShouldBe(2);
        saved.HoursWorked.ShouldBe(8m);
        saved.Projects.Select(p => p.Project.Name).OrderBy(n => n).ShouldBe(["Alpha", "Beta"]);
    }

    [Test]
    public async Task GenerateInvoiceAsync_DailyRateWithoutProjects_FallsBackToPerDayLineItems()
    {
        GivenRate(RateType.Daily, 400m);

        var dto = MonthlyDto(
            new WorkDayDto(new DateOnly(2026, 1, 1), DayType.Worked),
            new WorkDayDto(new DateOnly(2026, 1, 2), DayType.Worked));

        await _service.GenerateInvoiceAsync(dto, TestContext.CurrentContext.CancellationToken);

        _capturedModel.LineItems.Count.ShouldBe(2);
        _capturedModel.LineItems.ShouldAllBe(l => l.Quantity == 1m);
        _capturedModel.ProjectSummary.ShouldBeEmpty();
    }

    [TestCase("it-IT")]
    [TestCase("es-ES")]
    public async Task GenerateInvoiceAsync_Always_ExposesTheCustomerLocale(string locale)
    {
        GivenCustomerLocale(locale);
        GivenRate(RateType.Daily, 400m);

        await _service.GenerateInvoiceAsync(
            MonthlyDto(new WorkDayDto(new DateOnly(2026, 1, 1), DayType.Worked)),
            TestContext.CurrentContext.CancellationToken);

        _capturedModel.Locale.ShouldBe(locale);
    }

    [Test]
    public async Task PreviewInvoiceAsync_Always_ExposesTheCustomerLocale()
    {
        GivenCustomerLocale("it-IT");
        GivenRate(RateType.Daily, 400m);

        await _service.PreviewInvoiceAsync(
            MonthlyDto(new WorkDayDto(new DateOnly(2026, 1, 1), DayType.Worked)),
            TestContext.CurrentContext.CancellationToken);

        _capturedModel.Locale.ShouldBe("it-IT");
    }

    private void GivenCustomerLocale(string locale)
    {
        var customer = new Customer("Acme", "ACME123", new Address("Main", "1", "Town", "12345", "Country"), locale)
        {
            Id = CustomerId
        };
        _customerRepository
            .Setup(r => r.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
    }

    [Test]
    public async Task GenerateInvoiceAsync_HourlyRateWorkedDayWithoutHours_Throws()
    {
        GivenRate(RateType.Hourly, 50m);

        var dto = MonthlyDto(new WorkDayDto(new DateOnly(2026, 1, 1), DayType.Worked));

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _service.GenerateInvoiceAsync(dto, TestContext.CurrentContext.CancellationToken));
    }

    [Test]
    public async Task GenerateInvoiceAsync_DailyRateMixedAssignedAndUnassignedDays_AddsUnassignedLine()
    {
        GivenRate(RateType.Daily, 400m);

        var dto = MonthlyDto(
            WorkedDay(1, ("Alpha", 8m)),
            new WorkDayDto(new DateOnly(2026, 1, 2), DayType.Worked));

        var invoice = await _service.GenerateInvoiceAsync(dto, TestContext.CurrentContext.CancellationToken);

        var unassigned = _capturedModel.LineItems.Single(l => l.Description == "Unassigned");
        unassigned.Quantity.ShouldBe(1m);
        unassigned.Amount.ShouldBe(400m);
        _capturedModel.LineItems.Where(l => l.Description is "Alpha" or "Unassigned").Sum(l => l.Amount)
            .ShouldBe(invoice.Subtotal.Amount);
    }

    [Test]
    public async Task PreviewInvoiceAsync_Always_WritesNothing()
    {
        GivenRate(RateType.Hourly, 50m);
        var sequence = new InvoiceSequence(7);
        _sequenceRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sequence);

        var dto = MonthlyDto(WorkedDay(1, ("Alpha", 3m), ("Beta", 5m)));

        await _service.PreviewInvoiceAsync(dto, TestContext.CurrentContext.CancellationToken);

        sequence.CurrentValue.ShouldBe(7);
        _savedWorkDays.ShouldBeEmpty();
        _projectResolver.Verify(
            r => r.ResolveOrCreateAsync(It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _workDayRepository.Verify(r => r.DeleteAsync(It.IsAny<WorkDay>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task PreviewInvoiceAsync_Always_UsesTheNextSequenceNumber()
    {
        GivenRate(RateType.Daily, 400m);
        _sequenceRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceSequence(7));

        var draft = await _service.PreviewInvoiceAsync(
            MonthlyDto(WorkedDay(1, ("Alpha", 8m))), TestContext.CurrentContext.CancellationToken);

        draft.Invoice.Number.ToString().ShouldEndWith("-007");
    }

    [Test]
    public async Task PreviewInvoiceAsync_SameAllocationsAsGenerate_RendersTheSameModel()
    {
        GivenRate(RateType.Daily, 400m);
        var dto = MonthlyDto(
            WorkedDay(1, ("Alpha", 6m), ("Beta", 2m)),
            WorkedDay(2, ("Alpha", 8m)));

        var draft = await _service.PreviewInvoiceAsync(dto, TestContext.CurrentContext.CancellationToken);
        var previewLines = _capturedModel.LineItems.Select(l => (l.Description, l.Quantity, l.Amount)).ToList();

        var invoice = await _service.GenerateInvoiceAsync(dto, TestContext.CurrentContext.CancellationToken);

        draft.Invoice.Total.Amount.ShouldBe(invoice.Total.Amount);
        _capturedModel.LineItems.Select(l => (l.Description, l.Quantity, l.Amount)).ToList().ShouldBe(previewLines);
    }

    [Test]
    public async Task PreviewInvoiceAsync_SameProjectTwiceOnADay_MergesTheAllocations()
    {
        GivenRate(RateType.Hourly, 50m);

        var draft = await _service.PreviewInvoiceAsync(
            MonthlyDto(WorkedDay(1, ("Alpha", 3m), (" alpha ", 2m))), TestContext.CurrentContext.CancellationToken);

        var allocation = draft.WorkDays.Single().Projects!.ShouldHaveSingleItem();
        allocation.ProjectName.ShouldBe("Alpha");
        allocation.Hours.ShouldBe(5m);
    }
}
