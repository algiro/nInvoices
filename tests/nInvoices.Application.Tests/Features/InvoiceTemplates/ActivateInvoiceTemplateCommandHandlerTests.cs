using System.Linq.Expressions;
using Moq;
using nInvoices.Application.Features.InvoiceTemplates.Commands;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Application.Tests.Features.InvoiceTemplates;

[TestFixture]
public sealed class ActivateInvoiceTemplateCommandHandlerTests
{
    private Mock<IRepository<InvoiceTemplate>> _repository = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private ActivateInvoiceTemplateCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IRepository<InvoiceTemplate>>();
        _unitOfWork = new Mock<IUnitOfWork>();
        // Run the operation like the real unit of work does; the transaction itself is tested there
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> operation, CancellationToken ct) => operation(ct));
        _handler = new ActivateInvoiceTemplateCommandHandler(_repository.Object, _unitOfWork.Object);
    }

    private static InvoiceTemplate Template(long id, bool active, long? customerId = 1) =>
        new(customerId, InvoiceType.Monthly, $"Template {id}", "<p>[[ total ]]</p>") { Id = id, IsActive = active };

    [Test]
    public async Task Handle_WhenTemplateMissing_ReturnsFalse()
    {
        _repository
            .Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvoiceTemplate?)null);

        var result = await _handler.Handle(new ActivateInvoiceTemplateCommand(9), TestContext.CurrentContext.CancellationToken);

        result.ShouldBeFalse();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_DeactivatesPreviousActiveTemplateBeforeActivating()
    {
        var current = Template(3, active: true);
        var next = Template(4, active: false);
        _repository
            .Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(next);
        _repository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<InvoiceTemplate, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<InvoiceTemplate, bool>> predicate, CancellationToken _) =>
                new[] { current, next }.Where(predicate.Compile()).ToList());

        // Record what each save would write: the unique index allows only one active row at a time
        var savedStates = new List<(bool Current, bool Next)>();
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => savedStates.Add((current.IsActive, next.IsActive)))
            .ReturnsAsync(1);

        var result = await _handler.Handle(new ActivateInvoiceTemplateCommand(4), TestContext.CurrentContext.CancellationToken);

        result.ShouldBeTrue();
        current.IsActive.ShouldBeFalse();
        next.IsActive.ShouldBeTrue();
        savedStates.ShouldBe([(false, false), (false, true)]);
        savedStates.ShouldAllBe(s => !(s.Current && s.Next));
        _unitOfWork.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_WhenSaveFails_ThrowsSoTheTransactionRollsBack()
    {
        var next = Template(4, active: false);
        _repository
            .Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(next);
        _repository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<InvoiceTemplate, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => _handler.Handle(new ActivateInvoiceTemplateCommand(4), TestContext.CurrentContext.CancellationToken));

        next.IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task Handle_SharedTemplate_ReplacesOnlyTheActiveSharedOneNotTheCustomers()
    {
        var customerActive = Template(2, active: true, customerId: 1);
        var sharedActive = Template(3, active: true, customerId: null);
        var next = Template(4, active: false, customerId: null);
        _repository
            .Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(next);
        _repository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<InvoiceTemplate, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<InvoiceTemplate, bool>> predicate, CancellationToken _) =>
                new[] { customerActive, sharedActive, next }.Where(predicate.Compile()).ToList());
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _handler.Handle(new ActivateInvoiceTemplateCommand(4), TestContext.CurrentContext.CancellationToken);

        result.ShouldBeTrue();
        next.IsActive.ShouldBeTrue();
        sharedActive.IsActive.ShouldBeFalse();
        customerActive.IsActive.ShouldBeTrue();
    }

    [Test]
    public async Task Handle_CustomerTemplate_LeavesTheSharedActiveOneAlone()
    {
        var sharedActive = Template(3, active: true, customerId: null);
        var next = Template(4, active: false, customerId: 1);
        _repository
            .Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(next);
        _repository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<InvoiceTemplate, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<InvoiceTemplate, bool>> predicate, CancellationToken _) =>
                new[] { sharedActive, next }.Where(predicate.Compile()).ToList());
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await _handler.Handle(new ActivateInvoiceTemplateCommand(4), TestContext.CurrentContext.CancellationToken);

        next.IsActive.ShouldBeTrue();
        sharedActive.IsActive.ShouldBeTrue();
    }
}
