using Moq;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Invoices.Queries;
using nInvoices.Application.Features.Invoices.Validators;
using nInvoices.Application.Services;
using nInvoices.Core.Enums;
using Shouldly;

namespace nInvoices.Application.Tests.Features.Invoices;

[TestFixture]
public sealed class PreviewInvoiceDraftQueryHandlerTests
{
    [Test]
    public async Task Handle_WithInvalidInput_ReturnsTheProblemsWithoutBuildingTheInvoice()
    {
        var generation = new Mock<IInvoiceGenerationService>(MockBehavior.Strict);
        var handler = new PreviewInvoiceDraftQueryHandler(
            generation.Object,
            Mock.Of<IMonthlyReportGenerationService>(),
            new GenerateInvoiceDtoValidator());
        var dto = new GenerateInvoiceDto
        {
            CustomerId = 1,
            InvoiceType = InvoiceType.OneTime,
            Expenses = [new ExpenseDto { Description = "Train", Amount = 0m, Currency = "EUR" }]
        };

        var preview = await handler.Handle(new PreviewInvoiceDraftQuery(dto), TestContext.CurrentContext.CancellationToken);

        preview.Errors.ShouldBe(["Amount must be positive"]);
        preview.InvoiceHtml.ShouldBeNull();
        generation.VerifyNoOtherCalls();
    }
}
