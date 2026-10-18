using Moq;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Compliance.Commands;
using nInvoices.Application.Features.TaxPrediction.Commands;
using nInvoices.Application.Features.TaxPrediction.Queries;
using nInvoices.Application.TaxPrediction;
using nInvoices.Countries.Spain.TaxPrediction;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Exceptions;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using Shouldly;
using static nInvoices.Countries.Spain.TaxPrediction.SpainTaxPredictionModule;

namespace nInvoices.Application.Tests.TaxPrediction;

/// <summary>
/// The tax prediction requests over in-memory records: the made-up half-year of
/// <see cref="SpainTaxPredictionTests"/> entered as invoices, tax lines, expenses and payments.
/// </summary>
[TestFixture]
public sealed class TaxPredictionHandlerTests
{
    private const long EuClient = 1;
    private const long CanarianClient = 2;

    private static readonly ITaxPredictionRegistry Registry = new TaxPredictionRegistry([new SpainTaxPredictionModule(TestText.Instance)]);

    private InMemoryRepository<TaxProfile> _profiles = null!;
    private InMemoryRepository<Tax> _taxes = null!;
    private InMemoryRepository<BusinessExpense> _expenses = null!;
    private InMemoryRepository<RecurringExpense> _recurring = null!;
    private InMemoryRepository<TaxPayment> _payments = null!;
    private List<Invoice> _invoices = null!;
    private Mock<IInvoiceRepository> _invoiceRepository = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public void SetUp()
    {
        _profiles = new InMemoryRepository<TaxProfile>();
        _taxes = new InMemoryRepository<Tax>();
        _expenses = new InMemoryRepository<BusinessExpense>();
        _recurring = new InMemoryRepository<RecurringExpense>();
        _payments = new InMemoryRepository<TaxPayment>();
        _invoices = [];
        _unitOfWork = new Mock<IUnitOfWork>();

        _invoiceRepository = new Mock<IInvoiceRepository>();
        _invoiceRepository
            .Setup(r => r.GetIssuedInYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int year, CancellationToken _) => _invoices
                .Where(i => i.IssueDate.Year == year && i.Status is InvoiceStatus.Finalized or InvoiceStatus.Sent or InvoiceStatus.Paid)
                .ToList());
        _invoiceRepository
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Invoice, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Invoice, bool>> predicate, CancellationToken _) =>
                _invoices.Where(predicate.Compile()).ToList());
    }

    private void SetUpGestorProfile() =>
        _profiles.Items.Add(new TaxProfile("ES", SimplifiedDirectEstimation, new Dictionary<string, string>
        {
            [TerritoryKey] = CanaryIslands,
            [DifficultExpensesKey] = "true"
        }));

    private static Invoice Issued(long customerId, int month, int day, decimal subtotal, string currency = "EUR", bool finalize = true)
    {
        var invoice = new Invoice(customerId, new InvoiceNumber($"26-{month:00}-{day:00}-{customerId}"), InvoiceType.Monthly,
            new DateOnly(2026, month, day), new Money(subtotal, currency), currency);
        if (finalize)
            invoice.Finalize(invoice.Number);
        return invoice;
    }

    /// <summary>The Canarian client's invoice: 7% IGIC added, 15% IRPF withheld, as the tax lines record them.</summary>
    private static Invoice IssuedWithIgicAndIrpf(int month, int day, decimal subtotal)
    {
        var invoice = Issued(CanarianClient, month, day, subtotal);
        invoice.TaxLines.Add(new InvoiceTaxLine("IGIC", "IGIC 7%", 7m, new Money(subtotal, "EUR"), new Money(subtotal * 0.07m, "EUR"), 0));
        invoice.TaxLines.Add(new InvoiceTaxLine("IRPF", "IRPF -15%", -15m, new Money(subtotal, "EUR"), new Money(subtotal * -0.15m, "EUR"), 1));
        invoice.AddTaxes(new Money(subtotal * -0.08m, "EUR"));
        return invoice;
    }

    private void SeedHalfYear()
    {
        var igic = new Tax(CanarianClient, "IGIC", "IGIC 7%", "PERCENTAGE", 7m, TaxApplicationType.OnSubtotal);
        igic.SetComplianceValues("ES", new Dictionary<string, string> { ["taxType"] = "IGIC" });
        _taxes.Items.AddRange([igic, new Tax(CanarianClient, "IRPF", "IRPF -15%", "PERCENTAGE", -15m, TaxApplicationType.OnSubtotal, 1)]);

        _invoices.AddRange(
        [
            Issued(EuClient, 1, 31, 4000m),
            Issued(EuClient, 2, 28, 4000m),
            Issued(EuClient, 3, 31, 4000m),
            IssuedWithIgicAndIrpf(3, 31, 3000m),
            Issued(EuClient, 4, 30, 4200m),
            IssuedWithIgicAndIrpf(4, 30, 1000m),
            Issued(EuClient, 5, 31, 4000m),
            Issued(EuClient, 6, 30, 3600m)
        ]);

        foreach (var (month, amount) in new[] { (1, 12.40m), (2, 250.00m), (3, 18.60m), (4, 30.00m), (5, 40.00m), (6, 25.00m) })
            _expenses.Items.Add(new BusinessExpense(new DateOnly(2026, month, 15), "Expense", new Money(amount, "EUR")));

        _recurring.Items.AddRange(
        [
            new RecurringExpense("Social Security", new Money(300.00m, "EUR"), ExpenseCategory.SocialSecurity, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1)),
            new RecurringExpense("Social Security", new Money(500.00m, "EUR"), ExpenseCategory.SocialSecurity, new DateOnly(2026, 7, 1))
        ]);
    }

    private Task<TaxPredictionDto> PredictAsync(int year = 2026) =>
        new GetTaxPredictionQueryHandler(Registry, _profiles, _invoiceRepository.Object, _taxes, _expenses, _recurring, _payments, TestText.Instance)
            .Handle(new GetTaxPredictionQuery(year), Token).AsTask();

    private static TaxFormDto Form(TaxPredictionDto prediction, int quarter, string code) =>
        prediction.Periods[quarter - 1].Forms.Single(f => f.Code == code);

    [Test]
    public async Task GetTaxPrediction_HalfYear_MatchesTheCalculationOnPlainValues()
    {
        SetUpGestorProfile();
        SeedHalfYear();
        _payments.Items.Add(new TaxPayment(2026, 1, "130", new Money(2625.00m, "EUR"), new DateOnly(2026, 4, 20)));

        var prediction = await PredictAsync();

        prediction.IsSetUp.ShouldBeTrue();
        (prediction.CountryCode, prediction.Currency).ShouldBe(("ES", "EUR"));
        Form(prediction, 1, "130").Estimated.ShouldBe(2625.61m);
        Form(prediction, 1, "130").Payment.ShouldNotBeNull().Amount.ShouldBe(2625.00m);
        Form(prediction, 2, "130").Estimated.ShouldBe(2243.56m);
        Form(prediction, 1, "420").Estimated.ShouldBe(210m);
        Form(prediction, 2, "420").Estimated.ShouldBe(70m);
        prediction.Warnings.ShouldBeEmpty();
    }

    [Test]
    public async Task GetTaxPrediction_TaxNotMarkedIgicOrIva_CountsAsIgicInTheCanaryIslands()
    {
        // As most users have it: Spain's invoicing rules off, so the tax was never marked
        SetUpGestorProfile();
        SeedHalfYear();
        _taxes.Items.Single(t => t.TaxId == "IGIC").SetComplianceValues("ES", new Dictionary<string, string>());

        var prediction = await PredictAsync();

        Form(prediction, 1, "420").Estimated.ShouldBe(210m);
        Form(prediction, 2, "420").Estimated.ShouldBe(70m);
        prediction.Warnings.ShouldBeEmpty();
    }

    [Test]
    public async Task GetTaxPrediction_WithholdingLines_AreBox06_WhenSubtracted()
    {
        _profiles.Items.Add(new TaxProfile("ES", SimplifiedDirectEstimation, new Dictionary<string, string>
        {
            [TerritoryKey] = CanaryIslands,
            [DifficultExpensesKey] = "true",
            [DeductWithholdingsKey] = "true"
        }));
        SeedHalfYear();

        var q2 = Form(await PredictAsync(), 2, "130");

        q2.Lines.Single(l => l.Box == "06").Amount.ShouldBe(600m);
    }

    [Test]
    public async Task GetTaxPrediction_RecurringFee_IsCountedEveryMonthItRuns()
    {
        SetUpGestorProfile();
        SeedHalfYear();

        var q3 = Form(await PredictAsync(), 3, "130");

        // Recorded expenses to the end of September: 376.00 + 6 × 300.00 + 3 × 500.00
        q3.Lines.Single(l => l.Label.StartsWith("of which recorded", StringComparison.Ordinal)).Amount.ShouldBe(3676.00m);
    }

    [Test]
    public async Task GetTaxPrediction_DraftsAndOtherCurrencies_AreLeftOutWithAWarning()
    {
        SetUpGestorProfile();
        _invoices.AddRange([Issued(EuClient, 1, 31, 1000m), Issued(EuClient, 2, 28, 2000m, "USD"), Issued(EuClient, 3, 1, 500m, finalize: false)]);
        _expenses.Items.Add(new BusinessExpense(new DateOnly(2026, 2, 1), "Software", new Money(20m, "USD")));

        var prediction = await PredictAsync();

        Form(prediction, 1, "130").Lines.Single(l => l.Box == "01").Amount.ShouldBe(1000m);
        prediction.Warnings.ShouldBe(
        [
            "1 invoice(s) in a currency other than EUR are not counted.",
            "1 expense(s) in a currency other than EUR are not counted.",
            "1 draft invoice(s) dated 2026 are not counted until they are finalized."
        ]);
    }

    [Test]
    public async Task GetTaxPrediction_WithoutProfile_IsNotSetUp()
    {
        SeedHalfYear();

        var prediction = await PredictAsync();

        prediction.IsSetUp.ShouldBeFalse();
        prediction.Periods.ShouldBeEmpty();
        prediction.Warnings.ShouldHaveSingleItem().ShouldContain("Settings");
    }

    [Test]
    public async Task GetTaxPrediction_CountryNoLongerOffered_IsNotSetUp()
    {
        _profiles.Items.Add(new TaxProfile("PT", "simplified", null));

        (await PredictAsync()).IsSetUp.ShouldBeFalse();
    }

    [Test]
    public async Task GetSettings_OffersSpainWithItsDefaults_AndTheProfile()
    {
        SetUpGestorProfile();

        var settings = await new GetTaxPredictionSettingsQueryHandler(Registry, _profiles).Handle(new GetTaxPredictionSettingsQuery(), Token);

        var regime = settings.Countries.ShouldHaveSingleItem().Regimes.ShouldHaveSingleItem();
        regime.Fields.Select(f => f.Key).ShouldBe([TerritoryKey, DifficultExpensesKey, DeductWithholdingsKey]);
        regime.Defaults.ShouldBe(new Dictionary<string, string> { [DifficultExpensesKey] = "true", [DeductWithholdingsKey] = "true" });
        settings.Profile.ShouldNotBeNull().Values[TerritoryKey].ShouldBe(CanaryIslands);
    }

    private Task<TaxProfileDto> UpdateProfileAsync(UpdateTaxProfileDto dto) =>
        new UpdateTaxProfileCommandHandler(Registry, _profiles, _unitOfWork.Object, TestText.Instance).Handle(new UpdateTaxProfileCommand(dto), Token).AsTask();

    [Test]
    public async Task UpdateProfile_CreatesItOnce_ThenUpdatesIt_KeepingOnlyTheRegimeFields()
    {
        await UpdateProfileAsync(new UpdateTaxProfileDto("es", SimplifiedDirectEstimation,
            new Dictionary<string, string> { [TerritoryKey] = Mainland, ["unknown"] = "x", [DifficultExpensesKey] = "" }));
        var saved = await UpdateProfileAsync(new UpdateTaxProfileDto("ES", SimplifiedDirectEstimation,
            new Dictionary<string, string> { [TerritoryKey] = CanaryIslands, [DeductWithholdingsKey] = "true" }));

        _profiles.Items.ShouldHaveSingleItem();
        saved.CountryCode.ShouldBe("ES");
        saved.Values.ShouldBe(new Dictionary<string, string> { [TerritoryKey] = CanaryIslands, [DeductWithholdingsKey] = "true" });
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task UpdateProfile_MissingRequiredOrInvalidChoice_ReportsTheFields()
    {
        var missing = await Should.ThrowAsync<ComplianceValidationException>(() =>
            UpdateProfileAsync(new UpdateTaxProfileDto("ES", SimplifiedDirectEstimation, null)));
        var invalid = await Should.ThrowAsync<ComplianceValidationException>(() =>
            UpdateProfileAsync(new UpdateTaxProfileDto("ES", SimplifiedDirectEstimation,
                new Dictionary<string, string> { [TerritoryKey] = "ceuta", [DifficultExpensesKey] = "yes" })));

        missing.Issues.ShouldNotBeNull().Select(i => i.Field).ShouldBe([TerritoryKey]);
        invalid.Issues.ShouldNotBeNull().Select(i => i.Field).ShouldBe([TerritoryKey, DifficultExpensesKey]);
        _profiles.Items.ShouldBeEmpty();
    }

    [TestCase("PT", SimplifiedDirectEstimation)]
    [TestCase("ES", "modulos")]
    public async Task UpdateProfile_CountryOrRegimeNotOffered_Throws(string country, string regime)
    {
        await Should.ThrowAsync<DomainException>(() => UpdateProfileAsync(new UpdateTaxProfileDto(country, regime, null)));
    }

    private Task<TaxPaymentDto> RecordAsync(RecordTaxPaymentDto dto) =>
        new RecordTaxPaymentCommandHandler(Registry, _profiles, _payments, _unitOfWork.Object, TestText.Instance)
            .Handle(new RecordTaxPaymentCommand(dto), Token).AsTask();

    [Test]
    public async Task RecordPayment_SameFormAndPeriod_ReplacesTheRecord_InTheCountrysCurrency()
    {
        SetUpGestorProfile();

        await RecordAsync(new RecordTaxPaymentDto(2026, 1, "130", 3000m, new DateOnly(2026, 4, 19)));
        var payment = await RecordAsync(new RecordTaxPaymentDto(2026, 1, " 130 ", 2625.00m, new DateOnly(2026, 4, 20)));

        _payments.Items.ShouldHaveSingleItem();
        (payment.Amount, payment.Currency, payment.PaidOn).ShouldBe((2625.00m, "EUR", new DateOnly(2026, 4, 20)));
    }

    [Test]
    public async Task RecordPayment_WithoutProfile_Throws()
    {
        await Should.ThrowAsync<DomainException>(() => RecordAsync(new RecordTaxPaymentDto(2026, 1, "130", 1m, new DateOnly(2026, 4, 20))));
    }

    [Test]
    public async Task DeletePayment_RemovesIt_OrSaysItWasNotThere()
    {
        _payments.Items.Add(new TaxPayment(2026, 1, "130", new Money(1m, "EUR"), new DateOnly(2026, 4, 20)) { Id = 7 });
        var handler = new DeleteTaxPaymentCommandHandler(_payments, _unitOfWork.Object);

        (await handler.Handle(new DeleteTaxPaymentCommand(7), Token)).ShouldBeTrue();
        (await handler.Handle(new DeleteTaxPaymentCommand(7), Token)).ShouldBeFalse();
        _payments.Items.ShouldBeEmpty();
    }
}
