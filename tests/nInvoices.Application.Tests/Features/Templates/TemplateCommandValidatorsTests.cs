using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.EmailTemplates.Commands;
using nInvoices.Application.Features.EmailTemplates.Validators;
using nInvoices.Application.Features.MonthlyReportTemplates.Commands;
using nInvoices.Application.Features.MonthlyReportTemplates.Validators;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Application.Tests.Features.Templates;

/// <summary>Monthly report and email templates are checked with the engine that renders them.</summary>
[TestFixture]
public sealed class TemplateCommandValidatorsTests
{
    private const string ValidTemplate = "<h1>[[ customer.name ]]</h1>[[ for day in days ]]<p>[[ day.date ]]</p>[[ end ]]";
    private const string UnclosedLoop = "<h1>Report</h1>\n[[ for day in days ]]<p>[[ day.date ]]</p>";

    private ScribanTemplateRenderer _renderer = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public void SetUp() =>
        _renderer = new ScribanTemplateRenderer(
            NullLogger<ScribanTemplateRenderer>.Instance,
            Mock.Of<ILocalizationService>(),
            Mock.Of<IRepository<ImageAsset>>());

    [Test]
    public async Task CreateMonthlyReportTemplate_WithValidTemplate_IsValid()
    {
        var command = new CreateMonthlyReportTemplateCommand(new CreateMonthlyReportTemplateDto(null, "Timesheet", ValidTemplate));

        var result = await new CreateMonthlyReportTemplateCommandValidator(_renderer).ValidateAsync(command, Token);

        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public async Task CreateMonthlyReportTemplate_WithUnclosedLoop_ReportsTheSyntaxError()
    {
        var command = new CreateMonthlyReportTemplateCommand(new CreateMonthlyReportTemplateDto(null, "Timesheet", UnclosedLoop));

        var result = await new CreateMonthlyReportTemplateCommandValidator(_renderer).ValidateAsync(command, Token);

        var error = result.Errors.ShouldHaveSingleItem();
        error.PropertyName.ShouldBe("Template.Content");
        error.ErrorMessage.ShouldStartWith("Template syntax error: Line 2");
    }

    [Test]
    public async Task UpdateMonthlyReportTemplate_WithUnclosedLoop_IsInvalid()
    {
        var command = new UpdateMonthlyReportTemplateCommand(1, new UpdateMonthlyReportTemplateDto("Timesheet", UnclosedLoop));

        var result = await new UpdateMonthlyReportTemplateCommandValidator(_renderer).ValidateAsync(command, Token);

        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("Template.Content");
    }

    [Test]
    public async Task CreateEmailTemplate_WithValidSubjectAndBody_IsValid()
    {
        var command = new CreateEmailTemplateCommand(new CreateEmailTemplateDto(
            null, "Default", "Invoice [[ invoice.number ]]", ValidTemplate));

        var result = await new CreateEmailTemplateCommandValidator(_renderer).ValidateAsync(command, Token);

        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public async Task CreateEmailTemplate_WithBrokenSubject_NamesTheSubject()
    {
        var command = new CreateEmailTemplateCommand(new CreateEmailTemplateDto(
            null, "Default", "Invoice [[ invoice.number + ]]", ValidTemplate));

        var result = await new CreateEmailTemplateCommandValidator(_renderer).ValidateAsync(command, Token);

        var error = result.Errors.ShouldHaveSingleItem();
        error.PropertyName.ShouldBe("Template.Subject");
        error.ErrorMessage.ShouldStartWith("Subject syntax error: Line 1");
    }

    [Test]
    public async Task UpdateEmailTemplate_WithBrokenBody_NamesTheBody()
    {
        var command = new UpdateEmailTemplateCommand(1, new UpdateEmailTemplateDto("Default", "Invoice", UnclosedLoop));

        var result = await new UpdateEmailTemplateCommandValidator(_renderer).ValidateAsync(command, Token);

        var error = result.Errors.ShouldHaveSingleItem();
        error.PropertyName.ShouldBe("Template.Body");
        error.ErrorMessage.ShouldStartWith("Body syntax error: Line 2");
    }
}
