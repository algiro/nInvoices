using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using nInvoices.Api.Controllers;
using nInvoices.Application.Compliance;
using nInvoices.Application.DTOs;
using nInvoices.Core.Compliance;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.Encryption;
using Shouldly;

namespace nInvoices.Api.Tests.Controllers;

/// <summary>
/// A backup made on one server and restored on another (two in-memory SQLite databases) brings
/// back everything it holds: customers with their rates, taxes, projects, worked days and unbilled
/// expenses, invoices, and the user's settings and assets.
/// </summary>
[TestFixture]
public sealed class ImportExportRoundTripTests
{
    private const string User = "user-sub";

    // Same settings as the API's controllers (camelCase, enums as strings), so this is the file a user downloads
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly FieldEncryptor Encryptor = FieldEncryptor.CreateEphemeral();

    private SqliteConnection _source = null!;
    private SqliteConnection _target = null!;
    private readonly List<ApplicationDbContext> _contexts = [];

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private sealed class TestUser(string id) : IUserContext
    {
        public string? UserId => id;
        public string? Username => id;
        public string? Email => null;
        public IEnumerable<string> Roles => [];
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => false;
    }

    [SetUp]
    public async Task SetUp()
    {
        _source = new SqliteConnection("DataSource=:memory:");
        _target = new SqliteConnection("DataSource=:memory:");
        await _source.OpenAsync(Token);
        await _target.OpenAsync(Token);
        await Context(_source).Database.EnsureCreatedAsync(Token);
        await Context(_target).Database.EnsureCreatedAsync(Token);
    }

    [TearDown]
    public async Task TearDown()
    {
        foreach (var context in _contexts)
            await context.DisposeAsync();
        _contexts.Clear();
        await _source.DisposeAsync();
        await _target.DisposeAsync();
    }

    private ApplicationDbContext Context(SqliteConnection connection)
    {
        var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options, Encryptor, new TestUser(User));
        _contexts.Add(context);
        return context;
    }

    private static ImportExportController Controller(ApplicationDbContext context)
    {
        var registry = new Mock<IComplianceRegistry>();
        registry.Setup(r => r.Find("ES")).Returns(Mock.Of<ICountryComplianceModule>());
        return new ImportExportController(context, Mock.Of<IUnitOfWork>(), registry.Object, NullLogger<ImportExportController>.Instance);
    }

    private static T Ok<T>(ActionResult<T> result) =>
        result.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<T>();

    /// <summary>Through JSON and back, as the downloaded file is.</summary>
    private static DataExportDto ViaFile(DataExportDto data) =>
        JsonSerializer.Deserialize<DataExportDto>(JsonSerializer.Serialize(data, Json), Json)!;

    private async Task SeedSourceAsync()
    {
        var db = Context(_source);

        var customer = new Customer("Acme SL", "ESB12345678", new Address("Calle Mayor", "5", "Madrid", "28001", "Spain"), "es-ES");
        customer.SetComplianceValues("ES", new Dictionary<string, string> { ["dir3Office"] = "L01280796" });
        db.Customers.Add(customer);
        await db.SaveChangesAsync(Token);

        var daily = new Rate(customer.Id, RateType.Daily, new Money(450m, "EUR"));
        var senior = new Rate(customer.Id, RateType.Hourly, new Money(80m, "EUR"));
        senior.SetName("Senior");
        var retired = new Rate(customer.Id, RateType.Hourly, new Money(60m, "EUR")) { IsActive = false };
        retired.SetName("Junior");
        db.Rates.AddRange(daily, senior, retired);

        var vat = new Tax(customer.Id, "IVA", "IVA 21%", "PERCENTAGE", 21m, TaxApplicationType.OnSubtotal);
        vat.SetComplianceValues("ES", new Dictionary<string, string> { ["taxType"] = "01" });
        db.Taxes.Add(vat);

        var build = new Project(customer.Id, "Build");
        var legacy = new Project(customer.Id, "Legacy");
        legacy.Deactivate();
        db.Projects.AddRange(build, legacy);
        await db.SaveChangesAsync(Token);

        var worked = new WorkDay(customer.Id, new DateOnly(2026, 9, 1), DayType.Worked, 8m, "kickoff") { RateId = senior.Id };
        worked.Projects.Add(new WorkDayProject(build, 5m));
        worked.Projects.Add(new WorkDayProject(legacy, 3m));
        db.WorkDays.AddRange(worked, new WorkDay(customer.Id, new DateOnly(2026, 9, 2), DayType.PublicHoliday));
        db.Expenses.Add(new Expense(customer.Id, new DateOnly(2026, 9, 3), "Train to Sevilla", new Money(42.5m, "EUR")));

        var invoice = new Invoice(customer.Id, new InvoiceNumber("26-09-001"), InvoiceType.Monthly, new DateOnly(2026, 9, 30), new Money(640m, "EUR"), "EUR")
        {
            Status = InvoiceStatus.Finalized,
            Hours = 8m,
            RateId = senior.Id,
            Total = new Money(774.40m, "EUR"),
            TotalTaxes = new Money(134.40m, "EUR")
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(Token);
        db.InvoiceTaxLines.Add(new InvoiceTaxLine
        {
            InvoiceId = invoice.Id, TaxId = "IVA", Description = "IVA 21%", Rate = 21m,
            BaseAmount = new Money(640m, "EUR"), TaxAmount = new Money(134.40m, "EUR"), Order = 0
        });
        db.Expenses.Add(new Expense(customer.Id, new DateOnly(2026, 9, 15), "Hotel", new Money(100m, "EUR")) { InvoiceId = invoice.Id });

        var sequence = new InvoiceSequence(7);
        sequence.SetNumberFormat("{YEAR}-{NUMBER:000}");
        db.InvoiceSequences.Add(sequence);
        db.ImageAssets.Add(new ImageAsset("companyLogo", "logo.png", "image/png", "iVBORw0KGgo=", 8));
        var calendar = new HolidayCalendar("ES");
        calendar.Rules.Add(HolidayRule.Fixed("San Isidro", 5, 15));
        var dropped = HolidayRule.Fixed("Old holiday", 3, 1);
        dropped.Deactivate();
        calendar.Rules.Add(dropped);
        db.HolidayCalendars.Add(calendar);
        var compliance = new ComplianceSettings("ES");
        compliance.Update(true, "Freelancer Name", "12345678Z", new Address("Gran Via", "1", "Madrid", "28013", "Spain"),
            new Dictionary<string, string> { ["residence"] = "R" });
        db.ComplianceSettings.Add(compliance);
        await db.SaveChangesAsync(Token);
    }

    private async Task<DataExportDto> BackupAsync()
    {
        var controller = Controller(Context(_source));
        var settings = Ok(await controller.ExportSettings(Token));
        var customers = Ok(await controller.ExportCustomers(Token));
        var invoices = Ok(await controller.ExportInvoices(null, null, null, Token));
        return ViaFile(new DataExportDto("1.0", DateTime.UtcNow, customers.Customers, invoices.Invoices,
            customers.SharedTemplates, settings.Settings));
    }

    private async Task RestoreAsync(DataExportDto backup)
    {
        var controller = Controller(Context(_target));
        Ok(await controller.ImportSettings(backup, Token)).Errors.ShouldBeEmpty();
        Ok(await controller.ImportCustomers(backup, Token)).Errors.ShouldBeEmpty();
        Ok(await controller.ImportInvoices(backup, Token)).Errors.ShouldBeEmpty();
    }

    [Test]
    public async Task Restore_OnAnEmptyServer_BringsBackCustomersWithEverythingTheyOwn()
    {
        await SeedSourceAsync();

        await RestoreAsync(await BackupAsync());

        var db = Context(_target);
        var customer = await db.Customers.Include(c => c.Rates).Include(c => c.Taxes).Include(c => c.Projects).SingleAsync(Token);
        customer.Locale.ShouldBe("es-ES");
        customer.GetComplianceValues("ES")["dir3Office"].ShouldBe("L01280796");
        customer.Taxes.Single().GetComplianceValues("ES")["taxType"].ShouldBe("01");

        var rates = customer.Rates.OrderBy(r => r.Id).ToList();
        rates.Select(r => (r.Type, r.Name, r.Price.Amount, r.IsActive)).ShouldBe(
        [
            (RateType.Daily, (string?)null, 450m, true),
            (RateType.Hourly, "Senior", 80m, true),
            (RateType.Hourly, "Junior", 60m, false)
        ]);
        customer.Projects.Select(p => (p.Name, p.IsActive)).ShouldBe([("Build", true), ("Legacy", false)], ignoreOrder: true);

        var days = await db.WorkDays.Include(w => w.Projects).ThenInclude(p => p.Project).OrderBy(w => w.Date).ToListAsync(Token);
        days.Count.ShouldBe(2);
        days[0].HoursWorked.ShouldBe(8m);
        days[0].Notes.ShouldBe("kickoff");
        days[0].RateId.ShouldBe(rates[1].Id);
        days[0].Projects.Select(p => (p.Project.Name, p.Hours)).ShouldBe([("Build", 5m), ("Legacy", 3m)], ignoreOrder: true);
        days[1].DayType.ShouldBe(DayType.PublicHoliday);

        var unbilled = await db.Expenses.Where(e => e.InvoiceId == null).SingleAsync(Token);
        (unbilled.Description, unbilled.Amount.Amount).ShouldBe(("Train to Sevilla", 42.5m));
    }

    [Test]
    public async Task Restore_OnAnEmptyServer_BringsBackInvoicesWithTheirRateAndHours()
    {
        await SeedSourceAsync();

        await RestoreAsync(await BackupAsync());

        var db = Context(_target);
        var senior = await db.Rates.SingleAsync(r => r.Name == "Senior", Token);
        var invoice = await db.Invoices.Include(i => i.TaxLines).Include(i => i.Expenses).SingleAsync(Token);
        invoice.Number.Value.ShouldBe("26-09-001");
        invoice.Hours.ShouldBe(8m);
        invoice.RateId.ShouldBe(senior.Id);
        invoice.Total.Amount.ShouldBe(774.40m);
        invoice.TaxLines.Single().TaxAmount.Amount.ShouldBe(134.40m);
        invoice.Expenses.Single().Description.ShouldBe("Hotel");
    }

    [Test]
    public async Task Restore_OnAnEmptyServer_BringsBackSettingsAndAssets()
    {
        await SeedSourceAsync();

        await RestoreAsync(await BackupAsync());

        var db = Context(_target);
        var sequence = await db.InvoiceSequences.SingleAsync(Token);
        (sequence.CurrentValue, sequence.NumberFormat).ShouldBe((7, "{YEAR}-{NUMBER:000}"));
        (await db.ImageAssets.SingleAsync(Token)).Alias.ShouldBe("companyLogo");
        var calendar = await db.HolidayCalendars.Include(c => c.Rules).SingleAsync(Token);
        calendar.Rules.Select(r => (r.Name, r.IsActive)).ShouldBe([("San Isidro", true), ("Old holiday", false)], ignoreOrder: true);
        var compliance = await db.ComplianceSettings.SingleAsync(Token);
        compliance.IsEnabled.ShouldBeTrue();
        (compliance.LegalName, compliance.TaxId, compliance.Address!.City).ShouldBe(("Freelancer Name", "12345678Z", "Madrid"));
        compliance.Values["residence"].ShouldBe("R");
        compliance.HasCertificate.ShouldBeFalse();
    }

    [Test]
    public async Task Restore_Twice_AddsNothingTheSecondTime()
    {
        await SeedSourceAsync();
        var backup = await BackupAsync();
        await RestoreAsync(backup);

        var controller = Controller(Context(_target));
        var settings = Ok(await controller.ImportSettings(backup, Token));
        var customers = Ok(await controller.ImportCustomers(backup, Token));
        var invoices = Ok(await controller.ImportInvoices(backup, Token));

        (settings.Imported, customers.Imported, invoices.Imported).ShouldBe((0, 0, 0));
        var db = Context(_target);
        (await db.WorkDays.CountAsync(Token)).ShouldBe(2);
        (await db.ImageAssets.CountAsync(Token)).ShouldBe(1);
    }

    [Test]
    public async Task ImportSettings_SequenceAlreadyFurther_IsNotMovedBack()
    {
        await SeedSourceAsync();
        var backup = await BackupAsync();
        var target = Context(_target);
        target.InvoiceSequences.Add(new InvoiceSequence(20));
        await target.SaveChangesAsync(Token);

        Ok(await Controller(Context(_target)).ImportSettings(backup, Token));

        var sequence = await Context(_target).InvoiceSequences.SingleAsync(Token);
        sequence.CurrentValue.ShouldBe(20);
        sequence.NumberFormat.ShouldBe("{YEAR}-{NUMBER:000}");
    }

    [Test]
    public async Task ImportSettings_CountryNotOfferedHere_ImportsItTurnedOff()
    {
        var backup = ViaFile(new DataExportDto("1.0", DateTime.UtcNow, null, null, Settings: new UserSettingsExportDto(
            null, [], [], [new ComplianceSettingsExportDto("PT", true, "Nome", "123", null, new Dictionary<string, string>())])));

        Ok(await Controller(Context(_target)).ImportSettings(backup, Token)).Imported.ShouldBe(1);

        (await Context(_target).ComplianceSettings.SingleAsync(Token)).IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task ImportCustomers_FileFromBeforeTheseFields_StillImports()
    {
        const string oldFile = """
            {"exportVersion":"1.0","exportedAt":"2026-09-01T00:00:00Z",
             "customers":[{"name":"Old Ltd","fiscalId":"GB1","address":{"street":"High St","houseNumber":"1","city":"London","zipCode":"N1","country":"UK"},
               "createdAt":"2026-01-01T00:00:00Z","rates":[{"type":"Daily","price":{"amount":400,"currency":"GBP"},"createdAt":"2026-01-01T00:00:00Z"}],
               "taxes":[],"invoiceTemplates":[],"monthlyReportTemplates":[]}],
             "invoices":null}
            """;
        var data = JsonSerializer.Deserialize<DataExportDto>(oldFile, Json)!;

        Ok(await Controller(Context(_target)).ImportCustomers(data, Token)).Imported.ShouldBe(1);

        var customer = await Context(_target).Customers.Include(c => c.Rates).SingleAsync(Token);
        customer.Locale.ShouldBe("en-US");
        customer.Rates.Single().IsActive.ShouldBeTrue();
    }
}
