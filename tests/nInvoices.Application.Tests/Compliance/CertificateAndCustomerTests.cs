using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Moq;
using nInvoices.Application.Compliance;
using nInvoices.Application.Compliance.EInvoice;
using nInvoices.Application.Compliance.Spain;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Compliance.Commands;
using nInvoices.Application.Models;
using nInvoices.Application.Services;
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
public sealed class CertificateAndCustomerTests
{
    private InMemoryRepository<ComplianceSettings> _settings = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private FixedTimeProvider _time = null!;
    private ComplianceRegistry _registry = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    /// <summary>Marks what it protects, so a test can see the secret was not stored as it came.</summary>
    private sealed class MarkingProtector : ISecretProtector
    {
        public string Protect(string plaintext) => "protected:" + plaintext;
        public string Unprotect(string ciphertext) => ciphertext["protected:".Length..];
    }

    [SetUp]
    public void SetUp()
    {
        _settings = new InMemoryRepository<ComplianceSettings>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _time = new FixedTimeProvider(FacturaeTestData.Now.UtcDateTime);
        var spain = new SpainComplianceModule();
        _registry = new ComplianceRegistry(
            [spain],
            [new nInvoices.Application.Compliance.Spain.Facturae.FacturaeFormat(spain, _time)],
            Options.Create(new ComplianceOptions()));
    }

    private Task<ComplianceCountryDto?> Upload(string country, byte[] pfx, string password) =>
        new SetSigningCertificateCommandHandler(_registry, _settings, new MarkingProtector(), _unitOfWork.Object, _time)
            .Handle(new SetSigningCertificateCommand(country, new UploadCertificateDto(Convert.ToBase64String(pfx), password)), Token);

    private static byte[] Pfx(X509Certificate2 certificate, string password = "pw") =>
        certificate.Export(X509ContentType.Pfx, password);

    // --- Certificate ------------------------------------------------------------------------------

    [Test]
    public async Task Upload_ValidCertificate_IsStoredEncryptedAndDescribed()
    {
        using var certificate = FacturaeTestData.NewCertificate();

        var country = await Upload("ES", Pfx(certificate), "pw");

        country.ShouldNotBeNull();
        country.RequiresCertificate.ShouldBeTrue();
        country.Settings.Certificate.ShouldNotBeNull();
        country.Settings.Certificate.Subject.ShouldBe(certificate.Subject);
        country.Settings.Certificate.IsExpired.ShouldBeFalse();

        var saved = _settings.Items.ShouldHaveSingleItem();
        saved.ProtectedCertificate.ShouldStartWith("protected:");
        saved.ProtectedCertificatePassword.ShouldBe("protected:pw");
        saved.IsEnabled.ShouldBeFalse(); // uploading a certificate does not turn the regime on
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Upload_IntoExistingSettings_KeepsThem()
    {
        using var certificate = FacturaeTestData.NewCertificate();
        var existing = new ComplianceSettings("ES");
        existing.Update(true, "Ana", "12345678Z", null, new Dictionary<string, string>());
        _settings.Items.Add(existing);

        await Upload("ES", Pfx(certificate), "pw");

        var saved = _settings.Items.ShouldHaveSingleItem();
        saved.IsEnabled.ShouldBeTrue();
        saved.LegalName.ShouldBe("Ana");
        saved.HasCertificate.ShouldBeTrue();
    }

    [Test]
    public async Task Upload_WrongPassword_IsRejected()
    {
        using var certificate = FacturaeTestData.NewCertificate();

        var ex = await Should.ThrowAsync<ArgumentException>(() => Upload("ES", Pfx(certificate), "other"));

        ex.Message.ShouldContain("password");
        _settings.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task Upload_NotACertificateFile_IsRejected() =>
        await Should.ThrowAsync<ArgumentException>(() => Upload("ES", [1, 2, 3, 4], "pw"));

    [Test]
    public async Task Upload_CertificateWithoutPrivateKey_IsRejected()
    {
        using var certificate = FacturaeTestData.NewCertificate();
        var publicOnly = certificate.Export(X509ContentType.Cert);

        // A certificate alone is not a PKCS#12 file with a key
        await Should.ThrowAsync<ArgumentException>(() => Upload("ES", publicOnly, ""));
    }

    [Test]
    public async Task Upload_ExpiredCertificate_IsRejected()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Old", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var expired = request.CreateSelfSigned(FacturaeTestData.Now.AddYears(-2), FacturaeTestData.Now.AddYears(-1));

        var ex = await Should.ThrowAsync<ArgumentException>(() => Upload("ES", Pfx(expired), "pw"));

        ex.Message.ShouldContain("expired");
    }

    [Test]
    public async Task Upload_CountryNotOffered_ReturnsNull()
    {
        using var certificate = FacturaeTestData.NewCertificate();

        (await Upload("IT", Pfx(certificate), "pw")).ShouldBeNull();
        _settings.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task Remove_ClearsTheCertificate()
    {
        using var certificate = FacturaeTestData.NewCertificate();
        await Upload("ES", Pfx(certificate), "pw");

        var country = await new RemoveSigningCertificateCommandHandler(_registry, _settings, _unitOfWork.Object)
            .Handle(new RemoveSigningCertificateCommand("ES"), Token);

        country!.Settings.Certificate.ShouldBeNull();
        _settings.Items.Single().ProtectedCertificate.ShouldBeNull();
        _settings.Items.Single().ProtectedCertificatePassword.ShouldBeNull();
    }

    // --- Customer values --------------------------------------------------------------------------

    private static Customer NewCustomer() =>
        new("Ayuntamiento de Prueba", "Q2826000H", new Address("Plaza Mayor", "1", "Madrid", "28001", "Spain", "Madrid"));

    private CustomerCompliance CustomerComplianceWithSpainOn(bool on)
    {
        if (on)
        {
            var spain = new ComplianceSettings("ES");
            spain.Update(true, "Ana", "12345678Z", null, new Dictionary<string, string>());
            _settings.Items.Add(spain);
        }

        return new CustomerCompliance(new ComplianceGate(_registry, _settings));
    }

    [Test]
    public async Task CustomerValues_SpainOff_AreIgnored()
    {
        var customer = NewCustomer();

        await CustomerComplianceWithSpainOn(false).ApplyAsync(customer, new Dictionary<string, string> { ["ES.isPublicAdministration"] = "true" }, Token);

        customer.ComplianceValues.ShouldBeEmpty();
    }

    [Test]
    public async Task CustomerValues_SpainOn_AreStoredPrefixed()
    {
        var customer = NewCustomer();
        var values = new Dictionary<string, string>
        {
            ["ES.isPublicAdministration"] = "true",
            ["ES.dir3AccountingOffice"] = "L01280796",
            ["ES.dir3ManagingBody"] = "L01280796",
            ["ES.dir3ProcessingUnit"] = "GE0001234",
            ["ES.unknownField"] = "x"
        };

        await CustomerComplianceWithSpainOn(true).ApplyAsync(customer, values, Token);

        customer.ComplianceValues.Keys.ShouldBe(
            ["ES.isPublicAdministration", "ES.dir3AccountingOffice", "ES.dir3ManagingBody", "ES.dir3ProcessingUnit"], ignoreOrder: true);
        customer.GetComplianceValues("ES")[SpainComplianceModule.ProcessingUnitKey].ShouldBe("GE0001234");
    }

    [Test]
    public async Task CustomerValues_PublicAdministrationWithoutCodes_IsRejected()
    {
        var customer = NewCustomer();

        var ex = await Should.ThrowAsync<ComplianceValidationException>(() =>
            CustomerComplianceWithSpainOn(true).ApplyAsync(customer, new Dictionary<string, string> { ["ES.isPublicAdministration"] = "true" }, Token));

        ex.Issues.Count.ShouldBe(3);
        customer.ComplianceValues.ShouldBeEmpty();
    }

    [Test]
    public async Task CustomerValues_Null_ChangesNothing()
    {
        var customer = NewCustomer();
        customer.SetComplianceValues("ES", new Dictionary<string, string> { ["isPublicAdministration"] = "true" });

        await CustomerComplianceWithSpainOn(true).ApplyAsync(customer, null, Token);

        customer.ComplianceValues.ShouldContainKey("ES.isPublicAdministration");
    }

    [Test]
    public void CustomerValues_OfAnotherCountry_AreLeftAlone()
    {
        var customer = NewCustomer();
        customer.SetComplianceValues("IT", new Dictionary<string, string> { ["codiceDestinatario"] = "ABC1234" });

        customer.SetComplianceValues("ES", new Dictionary<string, string> { ["isPublicAdministration"] = "true" });

        customer.GetComplianceValues("IT")["codiceDestinatario"].ShouldBe("ABC1234");
        customer.GetComplianceValues("ES").ShouldContainKey("isPublicAdministration");
    }

    // --- Document factory ------------------------------------------------------------------------

    [Test]
    public async Task Document_TakesLinesFromTheInvoiceAndMapsNegativeTaxesToWithholdings()
    {
        var customer = NewCustomer();
        customer.Id = 1;
        var invoice = new Invoice(
            1, new InvoiceNumber("26-10-001"), InvoiceType.Monthly, new DateOnly(2026, 10, 3), new Money(8000m, "EUR"), "EUR")
        {
            Id = 5,
            DueDate = new DateOnly(2026, 11, 2),
            TaxLines =
            [
                new InvoiceTaxLine("VAT", "VAT 21%", 21m, new Money(8000m, "EUR"), new Money(1680m, "EUR"), 0),
                new InvoiceTaxLine("IRPF", "IRPF -15%", -15m, new Money(8000m, "EUR"), new Money(-1200m, "EUR"), 1)
            ]
        };
        invoice.AddTaxes(new Money(480m, "EUR"));

        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(r => r.GetByIdWithRelatedAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
        var generation = new Mock<IInvoiceGenerationService>();
        generation.Setup(g => g.BuildTemplateModelAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new InvoiceTemplateModel
        {
            LineItems = [new LineItemTemplateModel { Description = "Services", Quantity = 20, Rate = 400, Amount = 8000 }]
        });

        var settings = new ComplianceSettings("ES");
        var issuer = FacturaeTestData.Individual();
        settings.Update(true, issuer.LegalName, issuer.TaxId, issuer.Address, issuer.Values);
        customer.SetComplianceValues("ES", new Dictionary<string, string> { ["isPublicAdministration"] = "true" });

        var document = await new EInvoiceDocumentFactory(invoices.Object, new InMemoryRepository<Customer>(customer), generation.Object)
            .CreateAsync(5, settings, Token);

        document.Number.ShouldBe("26-10-001");
        document.Total.ShouldBe(8480m);
        document.Lines.ShouldHaveSingleItem().Amount.ShouldBe(8000m);
        document.Taxes.Count.ShouldBe(2);
        document.Taxes[0].ShouldBe(new EInvoiceTax(EInvoiceTaxKind.Added, "VAT 21%", 21m, 8000m, 1680m));
        document.Taxes[1].ShouldBe(new EInvoiceTax(EInvoiceTaxKind.Withheld, "IRPF -15%", 15m, 8000m, 1200m));
        document.Buyer.Values.ShouldContainKeyAndValue("isPublicAdministration", "true");
        document.Issuer.TaxId.ShouldBe("12345678Z");
        document.Language.ShouldBe("en");
    }
}
