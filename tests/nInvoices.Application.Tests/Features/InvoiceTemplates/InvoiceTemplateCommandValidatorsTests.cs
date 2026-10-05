using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.InvoiceTemplates.Commands;
using nInvoices.Application.Features.InvoiceTemplates.Validators;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Application.Tests.Features.InvoiceTemplates;

[TestFixture]
public sealed class InvoiceTemplateCommandValidatorsTests
{
    private ScribanTemplateRenderer _renderer = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public void SetUp() =>
        _renderer = new ScribanTemplateRenderer(
            NullLogger<ScribanTemplateRenderer>.Instance,
            Mock.Of<ILocalizationService>(),
            Mock.Of<IRepository<ImageAsset>>());

    [TestCase("<h1>[[ customer.name ]]</h1>[[ for line in lineItems ]]<p>[[ line.description ]]</p>[[ end ]]",
        TestName = "Loop with [[ ]] delimiters")]
    [TestCase("<style>@media print{.total{font-weight:bold}}</style><p>[[ invoice.number ]]</p>",
        TestName = "CSS with }} (rejected by the old brace count)")]
    [TestCase("<p>[[ if invoice.notes ]][[ invoice.notes ]][[ else ]]-[[ end ]]</p>",
        TestName = "If/else")]
    public async Task Create_WithValidScribanTemplate_IsValid(string content)
    {
        var result = await new CreateInvoiceTemplateCommandValidator(_renderer).ValidateAsync(Create(content), Token);

        result.IsValid.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Test]
    public async Task Create_WithUnclosedLoop_ReportsTheSyntaxErrorWithItsLine()
    {
        // The old check only counted {{ and }}: a broken [[ ]] template passed and failed at invoice time
        var content = "<h1>Invoice</h1>\n[[ for line in lineItems ]]<p>[[ line.description ]]</p>";

        var result = await new CreateInvoiceTemplateCommandValidator(_renderer).ValidateAsync(Create(content), Token);

        var error = result.Errors.ShouldHaveSingleItem();
        error.PropertyName.ShouldBe("Template.Content");
        error.ErrorMessage.ShouldStartWith("Template syntax error: Line 2");
    }

    [Test]
    public async Task Update_WithInvalidExpression_IsInvalid()
    {
        var command = new UpdateInvoiceTemplateCommand(1, new UpdateInvoiceTemplateDto("Default", "<p>[[ customer.name + ]]</p>", true));

        var result = await new UpdateInvoiceTemplateCommandValidator(_renderer).ValidateAsync(command, Token);

        result.Errors.Select(e => e.PropertyName).ShouldAllBe(p => p == "Template.Content");
        result.IsValid.ShouldBeFalse();
    }

    [Test]
    public async Task Create_WithBlankContent_ReportsOnlyTheRequiredRule()
    {
        var result = await new CreateInvoiceTemplateCommandValidator(_renderer).ValidateAsync(Create(""), Token);

        result.Errors.ShouldNotContain(e => e.ErrorMessage.StartsWith("Template syntax error"));
        result.Errors.ShouldContain(e => e.ErrorMessage == "Template content is required");
    }

    private static CreateInvoiceTemplateCommand Create(string content) =>
        new(new CreateInvoiceTemplateDto(null, InvoiceType.Monthly, "Default", content));
}
