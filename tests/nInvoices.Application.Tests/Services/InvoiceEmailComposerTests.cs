using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using nInvoices.Application.Services;
using nInvoices.Application.Services.Email;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.Services;

[TestFixture]
public sealed class InvoiceEmailComposerTests
{
    private Customer _customer = null!;
    private Invoice _invoice = null!;
    private InMemoryRepository<EmailTemplate> _templates = null!;
    private InvoiceEmailComposer _composer = null!;

    [SetUp]
    public void SetUp()
    {
        _customer = new Customer("ACME S.p.A.", "IT01234567890", new Address("Via Roma", "10", "Milano", "20121", "Italy"), "it-IT") { Id = 3 };
        _customer.SetContact("billing@acme.it", null);

        _invoice = new Invoice(3, new InvoiceNumber("26-09-001"), InvoiceType.Monthly, new DateOnly(2026, 10, 1), new Money(10000m, "EUR"), "EUR") { Id = 42 };
        _invoice.SetMonthlyInvoiceDetails(2026, 9, 20);
        _invoice.AddTaxes(new Money(2200m, "EUR"));

        _templates = new InMemoryRepository<EmailTemplate>();

        var images = new Mock<IRepository<ImageAsset>>();
        images.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var localization = new LocalizationService();
        var renderer = new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance, localization, images.Object);

        _composer = new InvoiceEmailComposer(
            _templates,
            new InMemoryRepository<Customer>(_customer),
            new InMemoryRepository<Invoice>(_invoice),
            renderer,
            localization,
            new Mock<IPdfExportService>().Object,
            new Mock<IHtmlToPdfConverter>().Object,
            new Mock<IMonthlyReportGenerationService>().Object);
    }

    private static CancellationToken Ct => TestContext.CurrentContext.CancellationToken;

    private EmailTemplate AddTemplate(long customerId, string name, bool active)
    {
        var template = new EmailTemplate(customerId, name, $"{name} subject", $"<p>{name}</p>");
        if (active)
            template.Activate();
        _templates.Items.Add(template);
        template.Id = _templates.Items.Count;
        return template;
    }

    [Test]
    public async Task ResolveTemplateAsync_WithoutTemplates_UsesBuiltInDefault()
    {
        var content = await _composer.ResolveTemplateAsync(3, null, Ct);

        content.TemplateId.ShouldBeNull();
        content.Subject.ShouldBe(DefaultEmailTemplate.Subject);
    }

    [Test]
    public async Task ResolveTemplateAsync_PrefersTheActiveTemplate()
    {
        AddTemplate(3, "Inactive", active: false);
        var active = AddTemplate(3, "Active", active: true);

        var content = await _composer.ResolveTemplateAsync(3, null, Ct);

        content.TemplateId.ShouldBe(active.Id);
    }

    [Test]
    public async Task ResolveTemplateAsync_UsesTheRequestedTemplateOnlyIfItBelongsToTheCustomer()
    {
        var own = AddTemplate(3, "Own", active: false);
        var other = AddTemplate(9, "Other customer", active: false);

        (await _composer.ResolveTemplateAsync(3, own.Id, Ct)).TemplateId.ShouldBe(own.Id);
        (await _composer.ResolveTemplateAsync(3, other.Id, Ct)).TemplateId.ShouldBeNull();
    }

    [Test]
    public async Task RenderAsync_DefaultTemplate_FillsInvoiceAndLocalizedMonth()
    {
        var model = _composer.BuildModel(_invoice, _customer, "me@gmail.com");

        var email = await _composer.RenderAsync(DefaultEmailTemplate.Subject, DefaultEmailTemplate.Body, model, Ct);

        email.Errors.ShouldBeEmpty();
        email.Subject.ShouldBe("Invoice 26-09-001 - ACME S.p.A.");
        email.Html.ShouldNotBeNull();
        email.Html.ShouldContain("Dear ACME S.p.A.");
        email.Html.ShouldContain("12.200,00 EUR");
        email.Html.ShouldContain("settembre 2026");
    }

    [Test]
    public async Task RenderAsync_SubjectWithLineBreaks_IsCollapsedToOneLine()
    {
        var model = _composer.BuildModel(_invoice, _customer, null);

        var email = await _composer.RenderAsync("Invoice\n  [[ invoiceNumber ]]\r\n", "<p>x</p>", model, Ct);

        email.Subject.ShouldBe("Invoice 26-09-001");
    }

    [Test]
    public async Task RenderAsync_SyntaxErrors_AreReportedPerPart()
    {
        var model = _composer.BuildModel(_invoice, _customer, null);

        var email = await _composer.RenderAsync("[[ if ]]", "<p>[[ for ]]</p>", model, Ct);

        email.Succeeded.ShouldBeFalse();
        email.Subject.ShouldBeNull();
        email.Errors.ShouldContain(e => e.StartsWith("Subject: "));
        email.Errors.ShouldContain(e => e.StartsWith("Body: "));
    }

    [Test]
    public async Task PreviewAsync_UsesTheCustomersLatestInvoice()
    {
        var preview = await _composer.PreviewAsync("[[ invoiceNumber ]] for [[ customer.email ]]", "<p>[[ total ]]</p>", 3, Ct);

        preview.Errors.ShouldBeEmpty();
        preview.Subject.ShouldBe("26-09-001 for billing@acme.it");
    }
}
