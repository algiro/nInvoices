using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Moq;
using nInvoices.Application.Compliance;
using nInvoices.Application.Compliance.EInvoice;
using nInvoices.Application.Compliance.Spain;
using nInvoices.Application.Compliance.Spain.Face;
using nInvoices.Application.Compliance.Spain.Facturae;
using nInvoices.Application.Tests.Compliance.Spain;
using nInvoices.Application.Services.Email;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance;

[TestFixture]
public sealed class EInvoiceDeliveryServiceTests
{
    private const long InvoiceId = 7;

    private InMemoryRepository<ComplianceSettings> _settings = null!;
    private InMemoryRepository<InvoiceEInvoice> _files = null!;
    private InMemoryRepository<EInvoiceSubmission> _submissions = null!;
    private InMemoryRepository<Customer> _customers = null!;
    private Mock<IInvoiceRepository> _invoices = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private Mock<IFaceClient> _face = null!;
    private FixedTimeProvider _time = null!;
    private Invoice _invoice = null!;
    private FaceOptions _options = null!;
    private EInvoiceDeliveryService _service = null!;
    private X509Certificate2 _certificate = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private sealed class PlainProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string ciphertext) => ciphertext;
    }

    private static FaceInvoice Registered(string status = "1200", string name = "Registrada") =>
        new("REGAGE26e000001", new DateTime(2026, 10, 3, 7, 31, 5, DateTimeKind.Utc), "26", "10-001", status, name, "4100", "No solicitada anulación",
            "Ayuntamiento de Prueba", "L01280796", "GE0001234", "L01280796");

    [SetUp]
    public void SetUp()
    {
        _certificate = FacturaeTestData.NewCertificate();
        _time = new FixedTimeProvider(FacturaeTestData.Now.UtcDateTime);
        _settings = new InMemoryRepository<ComplianceSettings>();
        _files = new InMemoryRepository<InvoiceEInvoice>();
        _submissions = new InMemoryRepository<EInvoiceSubmission>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _options = new FaceOptions { Environment = "Test" };

        var customer = new Customer("Ayuntamiento de Prueba", "Q2826000H", new Address("Plaza Mayor", "1", "Madrid", "28001", "Spain", "Madrid")) { Id = 1 };
        customer.SetComplianceValues("ES", new Dictionary<string, string>
        {
            [SpainComplianceModule.IsPublicAdministrationKey] = "true",
            [SpainComplianceModule.AccountingOfficeKey] = "L01280796",
            [SpainComplianceModule.ManagingBodyKey] = "L01280796",
            [SpainComplianceModule.ProcessingUnitKey] = "GE0001234"
        });
        _customers = new InMemoryRepository<Customer>(customer);

        _invoice = NewInvoice(InvoiceStatus.Finalized);
        _invoices = new Mock<IInvoiceRepository>();
        _invoices.Setup(r => r.GetByIdAsync(InvoiceId, It.IsAny<CancellationToken>())).ReturnsAsync(() => _invoice);

        _face = new Mock<IFaceClient>();
        _face
            .Setup(f => f.SendInvoiceAsync(It.IsAny<FaceTarget>(), It.IsAny<X509Certificate2>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Registered());
        _face
            .Setup(f => f.GetInvoiceAsync(It.IsAny<FaceTarget>(), It.IsAny<X509Certificate2>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Registered("2400", "Contabilizada la obligación de pago"));

        Rebuild();
    }

    [TearDown]
    public void TearDown() => _certificate.Dispose();

    private void Rebuild()
    {
        var spain = new SpainComplianceModule();
        var format = new FacturaeFormat(spain, _time);
        var registry = new ComplianceRegistry([spain], [format], Options.Create(new ComplianceOptions()));
        var gate = new ComplianceGate(registry, _settings);
        var channel = new FaceChannel(_face.Object, Options.Create(_options));

        _service = new EInvoiceDeliveryService(
            gate, [channel], _invoices.Object, _customers, _files, _submissions,
            new SigningCertificateLoader(new PlainProtector(), _time), _unitOfWork.Object, _time);
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

    /// <summary>The user turns Spain on; <paramref name="faceEmail"/> and the certificate can be left out.</summary>
    private void SpainOn(string? faceEmail = "ana@example.com", bool withCertificate = true)
    {
        var settings = new ComplianceSettings("ES");
        var issuer = FacturaeTestData.Individual();
        var values = new Dictionary<string, string>(issuer.Values);
        if (faceEmail is not null)
            values[SpainComplianceModule.FaceEmailKey] = faceEmail;
        settings.Update(true, issuer.LegalName, issuer.TaxId, issuer.Address, values);
        if (withCertificate)
        {
            settings.SetCertificate(
                Convert.ToBase64String(_certificate.Export(X509ContentType.Pfx, "pw")), "pw",
                _certificate.Subject, _certificate.Thumbprint, _certificate.NotAfter.ToUniversalTime());
        }
        _settings.Items.Add(settings);
    }

    private InvoiceEInvoice StoredFile()
    {
        var file = new InvoiceEInvoice(InvoiceId, "ES", FacturaeFormat.Id, [1, 2, 3], "application/xml", "xsig");
        _files.AddAsync(file).GetAwaiter().GetResult();
        return file;
    }

    // --- What the invoice can do ----------------------------------------------------------------

    [Test]
    public async Task NoCountryOn_NoChannels()
    {
        (await _service.GetChannelsAsync(InvoiceId, Token)).ShouldBeEmpty();
    }

    [Test]
    public async Task GetChannels_ReportsWhatIsMissing()
    {
        SpainOn(faceEmail: null);

        var status = (await _service.GetChannelsAsync(InvoiceId, Token)).ShouldHaveSingleItem();

        status.Channel.ChannelId.ShouldBe("face");
        status.Applies.ShouldBeTrue();
        status.HasFile.ShouldBeFalse();
        status.NotReady.ShouldHaveSingleItem().Field.ShouldBe(SpainComplianceModule.FaceEmailKey);
        status.Submission.ShouldBeNull();
    }

    [Test]
    public async Task GetChannels_CustomerThatIsNotAPublicAdministration_DoesNotApply()
    {
        SpainOn();
        _customers.Items[0].SetComplianceValues("ES", new Dictionary<string, string>());

        (await _service.GetChannelsAsync(InvoiceId, Token)).ShouldHaveSingleItem().Applies.ShouldBeFalse();
    }

    // --- Sending ---------------------------------------------------------------------------------

    [Test]
    public async Task Send_DeliversTheStoredFile_AndKeepsTheRegistryCode()
    {
        SpainOn();
        var file = StoredFile();

        var result = await _service.SendAsync(InvoiceId, "face", Token);

        result.Succeeded.ShouldBeTrue();
        var submission = _submissions.Items.ShouldHaveSingleItem();
        submission.InvoiceEInvoiceId.ShouldBe(file.Id);
        submission.ChannelId.ShouldBe("face");
        submission.Environment.ShouldBe("Test");
        submission.Reference.ShouldBe("REGAGE26e000001");
        submission.StatusCode.ShouldBe("1200");
        submission.StatusName.ShouldBe("Registrada");
        submission.RegisteredAt.ShouldBe(new DateTime(2026, 10, 3, 7, 31, 5, DateTimeKind.Utc));
        submission.SubmittedAt.ShouldBe(_time.GetUtcNow().UtcDateTime);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _face.Verify(f => f.SendInvoiceAsync(
            It.Is<FaceTarget>(t => !t.Production), It.IsAny<X509Certificate2>(), "ana@example.com",
            "26-10-001.xsig", It.Is<byte[]>(b => b.SequenceEqual(file.Content)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Send_ProductionEnvironment_GoesToProduction()
    {
        _options = new FaceOptions { Environment = "Production" };
        Rebuild();
        SpainOn();
        StoredFile();

        await _service.SendAsync(InvoiceId, "face", Token);

        _face.Verify(f => f.SendInvoiceAsync(
            It.Is<FaceTarget>(t => t.Production), It.IsAny<X509Certificate2>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _submissions.Items.ShouldHaveSingleItem().Environment.ShouldBe("Production");
    }

    [Test]
    public async Task Send_RefusedByFace_StoresNothing_SoItCanBeTriedAgain()
    {
        SpainOn();
        StoredFile();
        _face
            .Setup(f => f.SendInvoiceAsync(It.IsAny<FaceTarget>(), It.IsAny<X509Certificate2>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FaceException("El certificado no está dado de alta"));

        var result = await _service.SendAsync(InvoiceId, "face", Token);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldContain("no está dado de alta");
        _submissions.Items.ShouldBeEmpty();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Send_Twice_DoesNotSendAgain()
    {
        SpainOn();
        StoredFile();
        await _service.SendAsync(InvoiceId, "face", Token);

        var second = await _service.SendAsync(InvoiceId, "face", Token);

        second.Succeeded.ShouldBeFalse();
        second.Message.ShouldContain("already sent");
        second.Message.ShouldContain("REGAGE26e000001");
        _submissions.Items.ShouldHaveSingleItem();
        _face.Verify(f => f.SendInvoiceAsync(It.IsAny<FaceTarget>(), It.IsAny<X509Certificate2>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private async Task AssertRefusedWithoutCallingFace(string expected)
    {
        var result = await _service.SendAsync(InvoiceId, "face", Token);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldContain(expected, Case.Insensitive);
        _submissions.Items.ShouldBeEmpty();
        _face.Verify(f => f.SendInvoiceAsync(It.IsAny<FaceTarget>(), It.IsAny<X509Certificate2>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Send_UnknownChannel_IsRefused()
    {
        SpainOn();
        var result = await _service.SendAsync(InvoiceId, "nope", Token);
        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldContain("Unknown channel");
    }

    [Test]
    public async Task Send_SpainNotTurnedOn_IsRefused()
    {
        StoredFile();
        await AssertRefusedWithoutCallingFace("not turned on");
    }

    [Test]
    public async Task Send_FaceNotSetUpOnTheServer_IsRefused()
    {
        _options = new FaceOptions();
        Rebuild();
        SpainOn();
        StoredFile();

        await AssertRefusedWithoutCallingFace("not set up");
    }

    [Test]
    public async Task Send_Draft_IsRefused()
    {
        SpainOn();
        StoredFile();
        _invoice = NewInvoice(InvoiceStatus.Draft);

        await AssertRefusedWithoutCallingFace("Finalize");
    }

    [Test]
    public async Task Send_Cancelled_IsRefused()
    {
        SpainOn();
        StoredFile();
        _invoice = NewInvoice(InvoiceStatus.Cancelled);

        await AssertRefusedWithoutCallingFace("cancelled");
    }

    [Test]
    public async Task Send_WithoutAGeneratedFile_IsRefused()
    {
        SpainOn();
        await AssertRefusedWithoutCallingFace("Generate the e-invoice");
    }

    [Test]
    public async Task Send_WithoutTheFaceEmail_IsRefused()
    {
        SpainOn(faceEmail: null);
        StoredFile();

        await AssertRefusedWithoutCallingFace("email");
    }

    [Test]
    public async Task Send_WithoutACertificate_IsRefused()
    {
        SpainOn(withCertificate: false);
        StoredFile();

        await AssertRefusedWithoutCallingFace("certificate");
    }

    [Test]
    public async Task Send_CustomerThatIsNotAPublicAdministration_IsRefused()
    {
        SpainOn();
        StoredFile();
        _customers.Items[0].SetComplianceValues("ES", new Dictionary<string, string>());

        await AssertRefusedWithoutCallingFace("do not go through");
    }

    // --- Refreshing ------------------------------------------------------------------------------

    [Test]
    public async Task Refresh_StoresTheNewStatus()
    {
        SpainOn();
        StoredFile();
        await _service.SendAsync(InvoiceId, "face", Token);
        _time.Now = _time.Now.AddDays(3);

        var result = await _service.RefreshAsync(InvoiceId, "face", Token);

        result.Succeeded.ShouldBeTrue();
        var submission = _submissions.Items.ShouldHaveSingleItem();
        submission.StatusCode.ShouldBe("2400");
        submission.CheckedAt.ShouldBe(_time.GetUtcNow().UtcDateTime);
        submission.LastError.ShouldBeNull();
        _face.Verify(f => f.GetInvoiceAsync(It.IsAny<FaceTarget>(), It.IsAny<X509Certificate2>(), "REGAGE26e000001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Refresh_FaceUnreachable_KeepsTheLastStatus_AndNotesWhy()
    {
        SpainOn();
        StoredFile();
        await _service.SendAsync(InvoiceId, "face", Token);
        _face
            .Setup(f => f.GetInvoiceAsync(It.IsAny<FaceTarget>(), It.IsAny<X509Certificate2>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FaceException("FACe did not answer in time"));

        var result = await _service.RefreshAsync(InvoiceId, "face", Token);

        result.Succeeded.ShouldBeFalse();
        var submission = _submissions.Items.ShouldHaveSingleItem();
        submission.StatusCode.ShouldBe("1200");
        submission.LastError.ShouldBe("FACe did not answer in time");
    }

    [Test]
    public async Task Refresh_NotSent_IsRefused()
    {
        SpainOn();
        StoredFile();

        var result = await _service.RefreshAsync(InvoiceId, "face", Token);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldBe("It has not been sent");
    }

    // --- FACe channel on its own -----------------------------------------------------------------

    [Test]
    public void Channel_AppliesOnlyToPublicAdministrations()
    {
        var channel = new FaceChannel(_face.Object, Options.Create(_options));

        channel.AppliesTo(FacturaeTestData.PublicAdministration()).ShouldBeTrue();
        channel.AppliesTo(FacturaeTestData.Company()).ShouldBeFalse();
    }

    [TestCase("Test", "Test")]
    [TestCase("Production", "Production")]
    public void Channel_NamesItsEnvironment(string configured, string expected)
    {
        new FaceChannel(_face.Object, Options.Create(new FaceOptions { Environment = configured })).EnvironmentName.ShouldBe(expected);
    }

    [Test]
    public void Channel_WithoutEnvironment_IsUnavailable()
    {
        new FaceChannel(_face.Object, Options.Create(new FaceOptions())).UnavailableReason.ShouldNotBeNull();
        new FaceChannel(_face.Object, Options.Create(_options)).UnavailableReason.ShouldBeNull();
    }

    [TestCase("not-an-email")]
    [TestCase("")]
    public void Channel_InvalidEmail_IsNotReady(string email)
    {
        var channel = new FaceChannel(_face.Object, Options.Create(_options));
        var issuer = FacturaeTestData.Individual() with { Values = new Dictionary<string, string> { [SpainComplianceModule.FaceEmailKey] = email } };

        channel.CheckReady(issuer).ShouldHaveSingleItem();
    }
}
