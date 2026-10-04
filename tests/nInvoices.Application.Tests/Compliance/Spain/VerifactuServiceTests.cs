using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using Moq;
using nInvoices.Application.Compliance;
using nInvoices.Application.Compliance.Spain;
using nInvoices.Application.Compliance.Spain.Verifactu;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance.Spain;

/// <summary>An in-memory chain that, like the real one, hands back records by position.</summary>
internal sealed class FakeVerifactuRecords : IVerifactuRecordRepository
{
    public List<VerifactuRecord> Items { get; } = [];

    public Task<VerifactuRecord?> GetLastAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.OrderByDescending(r => r.Sequence).FirstOrDefault());

    public Task<IReadOnlyList<VerifactuRecord>> GetByInvoiceAsync(long invoiceId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VerifactuRecord>>(Items.Where(r => r.InvoiceId == invoiceId).OrderBy(r => r.Sequence).ToList());

    public Task<IReadOnlyList<VerifactuRecord>> GetChainAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VerifactuRecord>>(Items.OrderBy(r => r.Sequence).ToList());

    public Task<VerifactuRecord?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(r => r.Id == id));
    public Task<IEnumerable<VerifactuRecord>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<VerifactuRecord>>(Items.ToList());
    public Task<IEnumerable<VerifactuRecord>> FindAsync(Expression<Func<VerifactuRecord, bool>> predicate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<VerifactuRecord>>(Items.Where(predicate.Compile()).ToList());

    public Task<VerifactuRecord> AddAsync(VerifactuRecord entity, CancellationToken cancellationToken = default)
    {
        Items.Add(entity);
        return Task.FromResult(entity);
    }

    public Task UpdateAsync(VerifactuRecord entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task DeleteAsync(VerifactuRecord entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult(Items.Any(r => r.Id == id));
    public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(Items.Count);
}

[TestFixture]
public sealed class VerifactuServiceTests
{
    private const long InvoiceId = 5;

    private InMemoryRepository<ComplianceSettings> _settings = null!;
    private InMemoryRepository<Tax> _taxes = null!;
    private InMemoryRepository<VerifactuSubmission> _submissions = null!;
    private FakeVerifactuRecords _records = null!;
    private Mock<IInvoiceRepository> _invoices = null!;
    private FixedTimeProvider _time = null!;
    private Customer _customer = null!;
    private Invoice _invoice = null!;
    private VerifactuOptions _options = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private static VerifactuOptions Configured() => new()
    {
        Environment = "Test",
        ProducerName = "Producer SL",
        ProducerTaxId = "B12345674"
    };

    [SetUp]
    public void SetUp()
    {
        _time = new FixedTimeProvider(new DateTime(2026, 10, 3, 9, 30, 0, DateTimeKind.Utc));
        _settings = new InMemoryRepository<ComplianceSettings>();
        _taxes = new InMemoryRepository<Tax>();
        _submissions = new InMemoryRepository<VerifactuSubmission>();
        _records = new FakeVerifactuRecords();
        _options = Configured();

        _customer = new Customer("Cliente SA", "A58818501", new Address("Avinguda Diagonal", "100", "Barcelona", "08019", "Spain", "Barcelona")) { Id = 1 };
        _invoice = NewInvoice(InvoiceId, "26-10-001");

        _invoices = new Mock<IInvoiceRepository>();
        _invoices.Setup(r => r.GetByIdWithRelatedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken _) => id == _invoice.Id ? _invoice : null);
    }

    private static Invoice NewInvoice(long id, string number, decimal vatRate = 21m)
    {
        var invoice = new Invoice(1, new InvoiceNumber(number), InvoiceType.OneTime, new DateOnly(2026, 10, 3), new Money(8000m, "EUR"), "EUR") { Id = id };
        invoice.AddExpenses(new Money(100.55m, "EUR"));
        var tax = Math.Round(8100.55m * vatRate / 100m, 2);
        invoice.TaxLines.Add(new InvoiceTaxLine("VAT", $"VAT {vatRate}%", vatRate, new Money(8100.55m, "EUR"), new Money(tax, "EUR"), 0));
        invoice.AddTaxes(new Money(tax, "EUR"));
        invoice.FinalizeInvoice();
        return invoice;
    }

    private VerifactuService Service(bool verifactuOn = true, bool spainOn = true)
    {
        if (spainOn && _settings.Items.Count == 0)
        {
            var settings = new ComplianceSettings("ES");
            var values = new Dictionary<string, string> { [SpainComplianceModule.PersonTypeKey] = SpainComplianceModule.Individual };
            if (verifactuOn) values[SpainComplianceModule.VerifactuKey] = "true";
            settings.Update(true, "Ana Pérez García", "12345678Z",
                new Address("Calle Mayor", "1", "Madrid", "28013", "Spain", "Madrid"), values);
            _settings.Items.Add(settings);
        }

        var module = new SpainComplianceModule(Options.Create(_options));
        var registry = new ComplianceRegistry([module], [], Options.Create(new ComplianceOptions()));
        return new VerifactuService(
            new ComplianceGate(registry, _settings), _records, _invoices.Object, _taxes, _submissions, Options.Create(_options), _time);
    }

    // --- Off ------------------------------------------------------------------------------------

    [Test]
    public async Task VerifactuOff_NothingIsRecorded()
    {
        var service = Service(verifactuOn: false);

        (await service.IsActiveAsync(Token)).ShouldBeFalse();
        (await service.RecordIssuedAsync(_invoice, _customer, Token)).ShouldBeFalse();
        (await service.RecordCancelledAsync(_invoice, Token)).ShouldBeFalse();
        _records.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task SpainOff_NothingIsRecorded()
    {
        var service = Service(spainOn: false);

        (await service.RecordIssuedAsync(_invoice, _customer, Token)).ShouldBeFalse();
        _records.Items.ShouldBeEmpty();
    }

    // --- Issuing ----------------------------------------------------------------------------------

    [Test]
    public async Task Issued_FirstInvoice_StartsTheChain()
    {
        var service = Service();

        (await service.RecordIssuedAsync(_invoice, _customer, Token)).ShouldBeTrue();

        var record = _records.Items.ShouldHaveSingleItem();
        record.Sequence.ShouldBe(1);
        record.Kind.ShouldBe(VerifactuRecordKind.Issued);
        record.InvoiceId.ShouldBe(InvoiceId);
        record.IssuerTaxId.ShouldBe("12345678Z");
        record.InvoiceNumber.ShouldBe("26-10-001");
        record.IssueDate.ShouldBe("03-10-2026");
        record.InvoiceType.ShouldBe("F1");
        record.TotalTax.ShouldBe("1701.12");
        record.TotalAmount.ShouldBe("9801.67");
        record.PreviousHash.ShouldBe("");
        record.GeneratedAt.ShouldBe("2026-10-03T11:30:00+02:00");
        record.Hash.ShouldBe(VerifactuHash.Issued("12345678Z", "26-10-001", "03-10-2026", "F1", "1701.12", "9801.67", "", "2026-10-03T11:30:00+02:00"));
        record.Xml.ShouldContain("<PrimerRegistro>S</PrimerRegistro>");
    }

    [Test]
    public async Task EveryRecord_ComesWithItsPlaceInTheQueueForTheTaxAgency()
    {
        var service = Service();
        await service.RecordIssuedAsync(_invoice, _customer, Token);
        await service.RecordCancelledAsync(_invoice, Token);

        _submissions.Items.Count.ShouldBe(2);
        _submissions.Items.ShouldAllBe(s => s.Status == VerifactuSubmissionStatus.Pending);
        _submissions.Items.Select(s => s.Sequence).ShouldBe([1L, 2L]);
        _submissions.Items.ShouldAllBe(s => s.NextAttemptAt == _time.Now);
    }

    [Test]
    public async Task Issued_TheTotalLeavesOutWithholdings()
    {
        // 15% IRPF is held back from what is paid, but is not part of the total Verifactu reports
        _invoice.TaxLines.Add(new InvoiceTaxLine("IRPF", "IRPF", -15m, new Money(8100.55m, "EUR"), new Money(-1215.08m, "EUR"), 1));

        await Service().RecordIssuedAsync(_invoice, _customer, Token);

        _records.Items.Single().TotalAmount.ShouldBe("9801.67");
    }

    [Test]
    public async Task Issued_SecondInvoice_ChainsToTheFirst()
    {
        var service = Service();
        await service.RecordIssuedAsync(_invoice, _customer, Token);
        _time.Now = _time.Now.AddMinutes(5);
        var second = NewInvoice(6, "26-10-002");
        _invoices.Setup(r => r.GetByIdWithRelatedAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(second);

        await service.RecordIssuedAsync(second, _customer, Token);

        var first = _records.Items[0];
        var next = _records.Items[1];
        next.Sequence.ShouldBe(2);
        next.PreviousHash.ShouldBe(first.Hash);
        next.Xml.ShouldContain(first.Hash);
        VerifactuChainVerifier.Verify(_records.Items).IsIntact.ShouldBeTrue();
    }

    [Test]
    public async Task Issued_TwiceForTheSameInvoice_IsRecordedOnce()
    {
        var service = Service();

        await service.RecordIssuedAsync(_invoice, _customer, Token);
        (await service.RecordIssuedAsync(_invoice, _customer, Token)).ShouldBeFalse();

        _records.Items.Count.ShouldBe(1);
    }

    [Test]
    public async Task Issued_TimeNeverRunsBackwards()
    {
        var service = Service();
        await service.RecordIssuedAsync(_invoice, _customer, Token);
        var second = NewInvoice(6, "26-10-002");
        _invoices.Setup(r => r.GetByIdWithRelatedAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(second);
        _time.Now = _time.Now.AddMinutes(-10); // the clock steps back

        await service.RecordIssuedAsync(second, _customer, Token);

        _records.Items[1].GeneratedAt.ShouldBe(_records.Items[0].GeneratedAt);
    }

    [Test]
    public async Task Issued_ServerWithoutProducerData_RefusesToRecord()
    {
        _options = new VerifactuOptions();

        var ex = await Should.ThrowAsync<VerifactuException>(() => Service().RecordIssuedAsync(_invoice, _customer, Token));

        ex.Message.ShouldContain("not set up");
        _records.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task Issued_ZeroPercentTaxWithoutAReason_IsRefused()
    {
        _invoice = NewInvoice(InvoiceId, "26-10-001", vatRate: 0m);

        var ex = await Should.ThrowAsync<VerifactuException>(() => Service().RecordIssuedAsync(_invoice, _customer, Token));

        ex.Issues.ShouldContain(i => i.Message.Contains("0%"));
        _records.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task Issued_ZeroPercentTaxWithAReason_UsesIt()
    {
        _invoice = NewInvoice(InvoiceId, "26-10-001", vatRate: 0m);
        var tax = new Tax(1, "VAT", "VAT 0%", "PERCENTAGE", 0m, TaxApplicationType.OnSubtotal, 0);
        tax.SetComplianceValues("ES", new Dictionary<string, string> { [SpainComplianceModule.OperationKey] = "N2" });
        _taxes.Items.Add(tax);

        await Service().RecordIssuedAsync(_invoice, _customer, Token);

        var record = _records.Items.Single();
        record.Xml.ShouldContain("N2");
        record.TotalTax.ShouldBe("0.00");
        record.TotalAmount.ShouldBe("8100.55");
    }

    [Test]
    public async Task Issued_CustomerWithABadNif_IsRefused()
    {
        _customer.Update("Cliente SA", "A58818502", _customer.Address);

        var ex = await Should.ThrowAsync<VerifactuException>(() => Service().RecordIssuedAsync(_invoice, _customer, Token));

        ex.Issues.ShouldContain(i => i.Message.Contains("NIF"));
    }

    [Test]
    public async Task Issued_CustomerInTheEu_IsIdentifiedByItsVatNumber()
    {
        _customer = new Customer("Cliente Srl", "IT01234567890", new Address("Via Roma", "5", "Milano", "20121", "Italy", "MI")) { Id = 1 };
        _invoice = NewInvoice(InvoiceId, "26-10-001", vatRate: 0m);
        var tax = new Tax(1, "VAT", "VAT 0%", "PERCENTAGE", 0m, TaxApplicationType.OnSubtotal, 0);
        tax.SetComplianceValues("ES", new Dictionary<string, string> { [SpainComplianceModule.OperationKey] = "N2" });
        _taxes.Items.Add(tax);

        await Service().RecordIssuedAsync(_invoice, _customer, Token);

        var xml = _records.Items.Single().Xml;
        xml.ShouldContain("<CodigoPais>IT</CodigoPais>");
        xml.ShouldContain("IT01234567890");
    }

    [Test]
    public async Task Issued_InvoiceNumberThatIsNotPrintableAscii_IsRefused()
    {
        _invoice = NewInvoice(InvoiceId, "26-10-001ñ");

        await Should.ThrowAsync<VerifactuException>(() => Service().RecordIssuedAsync(_invoice, _customer, Token));
    }

    // --- Cancelling ---------------------------------------------------------------------------------

    [Test]
    public async Task Cancelled_RecordedInvoice_AddsACancellationToTheChain()
    {
        var service = Service();
        await service.RecordIssuedAsync(_invoice, _customer, Token);
        _time.Now = _time.Now.AddMinutes(2);

        (await service.RecordCancelledAsync(_invoice, Token)).ShouldBeTrue();

        var issued = _records.Items[0];
        var cancelled = _records.Items[1];
        cancelled.Kind.ShouldBe(VerifactuRecordKind.Cancelled);
        cancelled.Sequence.ShouldBe(2);
        cancelled.PreviousHash.ShouldBe(issued.Hash);
        cancelled.InvoiceNumber.ShouldBe("26-10-001");
        cancelled.Hash.ShouldBe(VerifactuHash.Cancelled("12345678Z", "26-10-001", "03-10-2026", issued.Hash, cancelled.GeneratedAt));
        VerifactuChainVerifier.Verify(_records.Items).IsIntact.ShouldBeTrue();
    }

    [Test]
    public async Task Cancelled_InvoiceThatWasNeverRecorded_AddsNothing()
    {
        (await Service().RecordCancelledAsync(_invoice, Token)).ShouldBeFalse();
        _records.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task Cancelled_Twice_IsRecordedOnce()
    {
        var service = Service();
        await service.RecordIssuedAsync(_invoice, _customer, Token);

        await service.RecordCancelledAsync(_invoice, Token);
        (await service.RecordCancelledAsync(_invoice, Token)).ShouldBeFalse();

        _records.Items.Count.ShouldBe(2);
    }

    // --- Deleting ---------------------------------------------------------------------------------

    [Test]
    public async Task Delete_RecordedInvoice_IsRefused()
    {
        var service = Service();
        await service.RecordIssuedAsync(_invoice, _customer, Token);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => service.EnsureCanDeleteAsync(_invoice, Token));

        ex.Message.ShouldContain("cancel it instead");
    }

    [Test]
    public async Task Delete_InvoiceWithoutRecords_IsAllowed() =>
        await Should.NotThrowAsync(() => Service().EnsureCanDeleteAsync(_invoice, Token));

    [Test]
    public async Task Delete_RecordedInvoice_IsRefusedEvenAfterVerifactuIsTurnedOff()
    {
        var service = Service();
        await service.RecordIssuedAsync(_invoice, _customer, Token);
        _settings.Items[0].Update(true, "Ana", "12345678Z", null, new Dictionary<string, string>());

        await Should.ThrowAsync<InvalidOperationException>(() => service.EnsureCanDeleteAsync(_invoice, Token));
    }

    // --- Verification and the QR -----------------------------------------------------------------

    [Test]
    public async Task QrUrl_UsesTheConfiguredEnvironment()
    {
        var service = Service();
        await service.RecordIssuedAsync(_invoice, _customer, Token);

        service.QrUrl(_records.Items[0]).ShouldBe(
            "https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR?nif=12345678Z&numserie=26-10-001&fecha=03-10-2026&importe=9801.67");

        _options = new VerifactuOptions { Environment = "Production", ProducerName = "P", ProducerTaxId = "B12345674" };
        Service().QrUrl(_records.Items[0]).ShouldStartWith("https://www2.agenciatributaria.gob.es/");
    }

    [Test]
    public async Task VerifyChain_ReportsTheChainOfTheUser()
    {
        var service = Service();
        await service.RecordIssuedAsync(_invoice, _customer, Token);

        var report = await service.VerifyChainAsync(Token);

        report.Records.ShouldBe(1);
        report.IsIntact.ShouldBeTrue();
    }

    // --- IGIC (Canary Islands) -------------------------------------------------------------------

    private void IgicTax(decimal rate, string? operation = null)
    {
        var tax = new Tax(1, "VAT", $"IGIC {rate}%", "PERCENTAGE", rate, TaxApplicationType.OnSubtotal, 0);
        var values = new Dictionary<string, string> { [SpainComplianceModule.TaxTypeKey] = SpainComplianceModule.Igic };
        if (operation is not null) values[SpainComplianceModule.OperationKey] = operation;
        tax.SetComplianceValues("ES", values);
        _taxes.Items.Add(tax);
    }

    [Test]
    public async Task Issued_Igic_RecordsTheTaxAsIgic()
    {
        _invoice = NewInvoice(InvoiceId, "26-10-001", vatRate: 7m);
        IgicTax(7m);

        (await Service().RecordIssuedAsync(_invoice, _customer, Token)).ShouldBeTrue();

        var record = _records.Items.Single();
        record.Xml.ShouldContain("<sf:Impuesto>03</sf:Impuesto>".Replace("sf:", ""), Case.Insensitive);
        record.TotalTax.ShouldBe("567.04");
        record.TotalAmount.ShouldBe("8667.59");
    }

    [Test]
    public async Task Issued_Igic_WithTheIrpfWithholdingBesideIt_Works()
    {
        _invoice = NewInvoice(InvoiceId, "26-10-001", vatRate: 7m);
        _invoice.TaxLines.Add(new InvoiceTaxLine("IRPF", "IRPF -15%", -15m, new Money(8100.55m, "EUR"), new Money(-1215.08m, "EUR"), 1));
        IgicTax(7m);

        (await Service().RecordIssuedAsync(_invoice, _customer, Token)).ShouldBeTrue();

        // The withholding is not part of what AEAT is told
        var record = _records.Items.Single();
        record.TotalTax.ShouldBe("567.04");
        record.TotalAmount.ShouldBe("8667.59");
    }

    [Test]
    public async Task Issued_Igic_ZeroRateTaxed_IsRecordedAsTaxed()
    {
        _invoice = NewInvoice(InvoiceId, "26-10-001", vatRate: 0m);
        IgicTax(0m, operation: "S1");

        (await Service().RecordIssuedAsync(_invoice, _customer, Token)).ShouldBeTrue();

        var record = _records.Items.Single();
        record.Xml.ShouldContain("S1");
        record.TotalTax.ShouldBe("0.00");
    }

    [Test]
    public async Task Issued_Igic_ExemptE7_IsAccepted()
    {
        _invoice = NewInvoice(InvoiceId, "26-10-001", vatRate: 0m);
        IgicTax(0m, operation: "E7");

        (await Service().RecordIssuedAsync(_invoice, _customer, Token)).ShouldBeTrue();
    }

    [Test]
    public async Task Issued_IvaWithE7_IsRefused_BecauseItExistsForIgicOnly()
    {
        _invoice = NewInvoice(InvoiceId, "26-10-001", vatRate: 0m);
        var tax = new Tax(1, "VAT", "VAT 0%", "PERCENTAGE", 0m, TaxApplicationType.OnSubtotal, 0);
        tax.SetComplianceValues("ES", new Dictionary<string, string> { [SpainComplianceModule.OperationKey] = "E7" });
        _taxes.Items.Add(tax);

        var ex = await Should.ThrowAsync<VerifactuException>(() => Service().RecordIssuedAsync(_invoice, _customer, Token));

        ex.Issues.ShouldContain(i => i.Message.Contains("E7") && i.Message.Contains("IGIC"));
    }
}
