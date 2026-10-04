using System.Security.Cryptography.X509Certificates;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using nInvoices.Application.Compliance;
using nInvoices.Application.Compliance.EInvoice;
using nInvoices.Application.Compliance.Spain;
using nInvoices.Application.Features.Invoices.Notifications;
using nInvoices.Application.Services.Email;
using nInvoices.Application.Tests.Compliance.Spain;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance;

[TestFixture]
public sealed class EInvoiceServiceTests
{
    private const long InvoiceId = 7;

    private InMemoryRepository<ComplianceSettings> _settings = null!;
    private InMemoryRepository<InvoiceEInvoice> _stored = null!;
    private InMemoryRepository<EInvoiceSubmission> _submissions = null!;
    private InMemoryRepository<Customer> _customers = null!;
    private Mock<IInvoiceRepository> _invoices = null!;
    private Mock<IEInvoiceDocumentFactory> _documents = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private FixedTimeProvider _time = null!;
    private Invoice _invoice = null!;
    private EInvoiceParty _buyer = null!;
    private EInvoiceService _service = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    /// <summary>Stands in for Data Protection: values are stored as they come.</summary>
    private sealed class PlainProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string ciphertext) => ciphertext;
    }

    [SetUp]
    public void SetUp()
    {
        _time = new FixedTimeProvider(FacturaeTestData.Now.UtcDateTime);
        _settings = new InMemoryRepository<ComplianceSettings>();
        _stored = new InMemoryRepository<InvoiceEInvoice>();
        _submissions = new InMemoryRepository<EInvoiceSubmission>();
        _unitOfWork = new Mock<IUnitOfWork>();

        _buyer = FacturaeTestData.PublicAdministration();
        _customers = new InMemoryRepository<Customer>(
            new Customer("Ayuntamiento de Prueba", "Q2826000H", new Address("Plaza Mayor", "1", "Madrid", "28001", "Spain", "Madrid")) { Id = 1 });

        _invoice = NewInvoice(InvoiceStatus.Finalized);
        _invoices = new Mock<IInvoiceRepository>();
        _invoices.Setup(r => r.GetByIdAsync(InvoiceId, It.IsAny<CancellationToken>())).ReturnsAsync(() => _invoice);

        _documents = new Mock<IEInvoiceDocumentFactory>();
        _documents
            .Setup(d => d.CreateAsync(InvoiceId, It.IsAny<ComplianceSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => FacturaeTestData.Invoice(buyer: _buyer));

        var spain = new SpainComplianceModule();
        var format = new nInvoices.Application.Compliance.Spain.Facturae.FacturaeFormat(spain, _time);
        var registry = new ComplianceRegistry([spain], [format], Options.Create(new ComplianceOptions()));
        var gate = new ComplianceGate(registry, _settings);

        _service = new EInvoiceService(
            gate, [format], _invoices.Object, _customers, _stored, _submissions, _documents.Object, new SigningCertificateLoader(new PlainProtector(), _time), _unitOfWork.Object);
    }

    private static Invoice NewInvoice(InvoiceStatus status)
    {
        var invoice = new Invoice(
            1, InvoiceNumber.Generate("{YEAR:yy}-{MONTH:00}-{NUMBER:000}", 1, new DateTime(2026, 10, 3), null),
            InvoiceType.Monthly, new DateOnly(2026, 10, 3), new Money(8100.55m, "EUR"), "EUR") { Id = InvoiceId };
        if (status != InvoiceStatus.Draft) invoice.FinalizeInvoice();
        if (status == InvoiceStatus.Cancelled) invoice.Cancel();
        return invoice;
    }

    /// <summary>The user turns Spain on, with a signing certificate unless <paramref name="certificate"/> is null.</summary>
    private void SpainOn(X509Certificate2? certificate, DateTime? notAfter = null)
    {
        var settings = new ComplianceSettings("ES");
        var issuer = FacturaeTestData.Individual();
        settings.Update(true, issuer.LegalName, issuer.TaxId, issuer.Address, issuer.Values);
        if (certificate is not null)
        {
            settings.SetCertificate(
                Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, "pw")), "pw",
                certificate.Subject, certificate.Thumbprint, notAfter ?? certificate.NotAfter.ToUniversalTime());
        }

        _settings.Items.Add(settings);
    }

    [Test]
    public async Task NoCountryOn_NothingApplies()
    {
        (await _service.GetStatusAsync(InvoiceId, Token)).ShouldBeEmpty();
        (await _service.GenerateAsync(InvoiceId, onlyMandatory: false, Token)).ShouldBeEmpty();

        _stored.Items.ShouldBeEmpty();
        _documents.Verify(d => d.CreateAsync(It.IsAny<long>(), It.IsAny<ComplianceSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Generate_Draft_Throws()
    {
        using var certificate = FacturaeTestData.NewCertificate();
        SpainOn(certificate);
        _invoice = NewInvoice(InvoiceStatus.Draft);

        await Should.ThrowAsync<InvalidOperationException>(() => _service.GenerateAsync(InvoiceId, false, Token));
    }

    [Test]
    public async Task Generate_Cancelled_Throws()
    {
        using var certificate = FacturaeTestData.NewCertificate();
        SpainOn(certificate);
        _invoice = NewInvoice(InvoiceStatus.Cancelled);

        await Should.ThrowAsync<InvalidOperationException>(() => _service.GenerateAsync(InvoiceId, false, Token));
    }

    [Test]
    public async Task Generate_PublicAdministration_StoresTheSignedFile()
    {
        using var certificate = FacturaeTestData.NewCertificate();
        SpainOn(certificate);

        var results = await _service.GenerateAsync(InvoiceId, onlyMandatory: true, Token);

        var result = results.ShouldHaveSingleItem();
        result.Generated.ShouldBeTrue();
        var file = _stored.Items.ShouldHaveSingleItem();
        file.FormatId.ShouldBe("facturae-3.2.2");
        file.CountryCode.ShouldBe("ES");
        file.FileExtension.ShouldBe("xsig");
        file.Sha256.Length.ShouldBe(64);
        FacturaeTestData.SchemaErrors(file.Content).ShouldBeEmpty();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Generate_Twice_ReplacesTheStoredFileInsteadOfAddingAnother()
    {
        using var certificate = FacturaeTestData.NewCertificate();
        SpainOn(certificate);

        await _service.GenerateAsync(InvoiceId, false, Token);
        _time.Now = _time.Now.AddHours(1);
        await _service.GenerateAsync(InvoiceId, false, Token);

        _stored.Items.ShouldHaveSingleItem();
    }

    [Test]
    public async Task Generate_AfterItWasSent_RefusesToChangeWhatWasDelivered()
    {
        using var certificate = FacturaeTestData.NewCertificate();
        SpainOn(certificate);
        await _service.GenerateAsync(InvoiceId, false, Token);
        var delivered = _stored.Items.Single();
        var content = delivered.Content.ToArray();
        _submissions.Items.Add(new EInvoiceSubmission(delivered.Id, "face", "Test", "REG1", _time.GetUtcNow().UtcDateTime));

        var result = (await _service.GenerateAsync(InvoiceId, false, Token)).ShouldHaveSingleItem();

        result.Generated.ShouldBeFalse();
        result.Issues.ShouldHaveSingleItem().Message.ShouldContain("already sent");
        _stored.Items.Single().Content.ShouldBe(content);
    }

    [Test]
    public async Task Generate_OnlyMandatory_SkipsACustomerThatIsNotAPublicAdministration()
    {
        using var certificate = FacturaeTestData.NewCertificate();
        SpainOn(certificate);
        _buyer = FacturaeTestData.Company();

        (await _service.GenerateAsync(InvoiceId, onlyMandatory: true, Token)).ShouldBeEmpty();
        _stored.Items.ShouldBeEmpty();

        // On request, the same invoice is generated: Facturae is voluntary for other customers
        (await _service.GenerateAsync(InvoiceId, onlyMandatory: false, Token)).ShouldHaveSingleItem().Generated.ShouldBeTrue();
    }

    [Test]
    public async Task Generate_WithoutCertificate_ReportsItAndStoresNothing()
    {
        SpainOn(certificate: null);

        var result = (await _service.GenerateAsync(InvoiceId, false, Token)).ShouldHaveSingleItem();

        result.Generated.ShouldBeFalse();
        result.Issues.ShouldContain(i => i.Message.Contains("signing certificate"));
        _stored.Items.ShouldBeEmpty();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Generate_ExpiredCertificate_IsReported()
    {
        using var certificate = FacturaeTestData.NewCertificate();
        SpainOn(certificate, notAfter: _time.Now.AddDays(-1));

        var result = (await _service.GenerateAsync(InvoiceId, false, Token)).ShouldHaveSingleItem();

        result.Generated.ShouldBeFalse();
        result.Issues.ShouldContain(i => i.Message.Contains("expired"));
    }

    [Test]
    public async Task Generate_InvoiceThatBreaksTheRules_ReportsTheIssuesAndStoresNothing()
    {
        using var certificate = FacturaeTestData.NewCertificate();
        SpainOn(certificate);
        _buyer = FacturaeTestData.PublicAdministration() with
        {
            Values = new Dictionary<string, string> { [SpainComplianceModule.IsPublicAdministrationKey] = "true" }
        };

        var result = (await _service.GenerateAsync(InvoiceId, false, Token)).ShouldHaveSingleItem();

        result.Generated.ShouldBeFalse();
        result.Issues.Count(i => i.Message.Contains("DIR3")).ShouldBe(3);
        _stored.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task GetStatus_ShowsTheFormatAndWhetherItWasGenerated()
    {
        using var certificate = FacturaeTestData.NewCertificate();
        SpainOn(certificate);
        _customers.Items[0].SetComplianceValues("ES", new Dictionary<string, string>
        {
            [SpainComplianceModule.IsPublicAdministrationKey] = "true"
        });

        var before = (await _service.GetStatusAsync(InvoiceId, Token)).ShouldHaveSingleItem();
        before.IsMandatory.ShouldBeTrue();
        before.Stored.ShouldBeNull();

        await _service.GenerateAsync(InvoiceId, false, Token);

        (await _service.GetStatusAsync(InvoiceId, Token)).ShouldHaveSingleItem().Stored.ShouldNotBeNull();
    }

    // --- The hook on finalize ---------------------------------------------------------------

    [Test]
    public async Task OnFinalized_GeneratesWhatIsMandatory_AndNeverThrows()
    {
        using var certificate = FacturaeTestData.NewCertificate();
        SpainOn(certificate);

        var handler = new GenerateMandatoryEInvoices(_service, NullLogger<GenerateMandatoryEInvoices>.Instance);
        await handler.Handle(new InvoiceFinalizedNotification(InvoiceId), Token);

        _stored.Items.ShouldHaveSingleItem();

        // A failure (here: the invoice cannot be found) is logged, not raised: the invoice is already final
        _invoices.Setup(r => r.GetByIdAsync(InvoiceId, It.IsAny<CancellationToken>())).ReturnsAsync((Invoice?)null);
        await Should.NotThrowAsync(() => handler.Handle(new InvoiceFinalizedNotification(InvoiceId), Token));
    }
}
