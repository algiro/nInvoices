using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.EmailTemplates.Commands;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Application.Tests.Features.Templates;

[TestFixture]
public sealed class EmailTemplateCommandHandlerTests
{
    private InMemoryRepository<EmailTemplate> _templates = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public void SetUp()
    {
        _templates = new InMemoryRepository<EmailTemplate>();
        _unitOfWork = new Mock<IUnitOfWork>();
    }

    [Test]
    public async Task Create_FirstTemplateOfACustomer_BecomesActive()
    {
        var created = await Create(customerId: 7);

        created.IsActive.ShouldBeTrue();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Create_WhenTheCustomerHasOne_StaysInactive()
    {
        await Create(customerId: 7);

        var second = await Create(customerId: 7);

        second.IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task Create_SharedTemplate_IsIndependentFromCustomerTemplates()
    {
        await Create(customerId: 7);

        var shared = await Create(customerId: null);

        shared.IsActive.ShouldBeTrue();
    }

    [Test]
    public async Task Update_WhenMissing_ReturnsNull()
    {
        var handler = new UpdateEmailTemplateCommandHandler(_templates, _unitOfWork.Object);

        var result = await handler.Handle(new UpdateEmailTemplateCommand(99, new UpdateEmailTemplateDto("N", "S", "B")), Token);

        result.ShouldBeNull();
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Update_ChangesNameSubjectAndBody()
    {
        var created = await Create(customerId: 7);
        var handler = new UpdateEmailTemplateCommandHandler(_templates, _unitOfWork.Object);

        var updated = await handler.Handle(
            new UpdateEmailTemplateCommand(created.Id, new UpdateEmailTemplateDto("Reminder", "Due: [[ invoice.number ]]", "<p>Body</p>")),
            Token);

        updated.ShouldNotBeNull();
        updated.Name.ShouldBe("Reminder");
        updated.Subject.ShouldBe("Due: [[ invoice.number ]]");
        updated.Body.ShouldBe("<p>Body</p>");
    }

    private async Task<EmailTemplateDto> Create(long? customerId)
    {
        var handler = new CreateEmailTemplateCommandHandler(
            _templates, _unitOfWork.Object, NullLogger<CreateEmailTemplateCommandHandler>.Instance);
        return await handler.Handle(
            new CreateEmailTemplateCommand(new CreateEmailTemplateDto(customerId, "Default", "Invoice [[ invoice.number ]]", "<p>Hello</p>")),
            Token);
    }
}
