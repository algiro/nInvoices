using Microsoft.Extensions.Options;
using Moq;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Invoices.Commands;
using nInvoices.Application.Features.Invoices.Queries;
using nInvoices.Application.Services;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Application.Tests.Features;

[TestFixture]
public sealed class InvoiceNumberingHandlerTests
{
    private Mock<IRepository<InvoiceSequence>> _sequences = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private Mock<IDraftInvoiceSynchronizer> _drafts = null!;
    private List<InvoiceSequence> _stored = null!;
    private IOptions<InvoiceSettings> _settings = null!;

    [SetUp]
    public void SetUp()
    {
        _stored = [];
        _settings = Options.Create(new InvoiceSettings { NumberFormat = "INV-{NUMBER:000}" });

        _sequences = new Mock<IRepository<InvoiceSequence>>();
        _sequences
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _stored.ToList());
        _sequences
            .Setup(r => r.AddAsync(It.IsAny<InvoiceSequence>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvoiceSequence s, CancellationToken _) => { _stored.Add(s); return s; });

        _unitOfWork = new Mock<IUnitOfWork>();
        _drafts = new Mock<IDraftInvoiceSynchronizer>();
    }

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private Task<InvoiceNumberingDto> Get() =>
        new GetInvoiceNumberingQueryHandler(_sequences.Object, _settings).Handle(new GetInvoiceNumberingQuery(), Token).AsTask();

    private Task<InvoiceNumberingDto> Update(int value, string? format) =>
        new UpdateInvoiceNumberingCommandHandler(_sequences.Object, _drafts.Object, _unitOfWork.Object, _settings)
            .Handle(new UpdateInvoiceNumberingCommand(new UpdateInvoiceNumberingDto(value, format)), Token).AsTask();

    [Test]
    public async Task Get_UserWithoutSequence_StartsAtOneWithTheDefaultPattern()
    {
        var numbering = await Get();

        numbering.CurrentValue.ShouldBe(1);
        numbering.NumberFormat.ShouldBe("INV-{NUMBER:000}");
        numbering.CustomNumberFormat.ShouldBeNull();
        numbering.DefaultNumberFormat.ShouldBe("INV-{NUMBER:000}");
        numbering.NextNumber.ShouldBe("INV-001");
    }

    [Test]
    public async Task Get_UserWithOwnPattern_ReportsItAsTheOneThatApplies()
    {
        var sequence = new InvoiceSequence(8);
        sequence.SetNumberFormat("{CUSTOMER:3}-{NUMBER:0000}");
        _stored.Add(sequence);

        var numbering = await Get();

        numbering.CurrentValue.ShouldBe(8);
        numbering.NumberFormat.ShouldBe("{CUSTOMER:3}-{NUMBER:0000}");
        numbering.CustomNumberFormat.ShouldBe("{CUSTOMER:3}-{NUMBER:0000}");
        numbering.NextNumber.ShouldBe("ACM-0008");
    }

    [Test]
    public async Task Update_UserWithoutSequence_CreatesIt()
    {
        var numbering = await Update(40, "{YEAR:yy}/{NUMBER:0000}");

        var sequence = _stored.ShouldHaveSingleItem();
        sequence.CurrentValue.ShouldBe(40);
        sequence.NumberFormat.ShouldBe("{YEAR:yy}/{NUMBER:0000}");
        numbering.NextNumber.ShouldEndWith("/0040");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        // The drafts show the next number, which this has just changed
        _drafts.Verify(d => d.RefreshDraftsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Update_ExistingSequence_ChangesItInPlace()
    {
        _stored.Add(new InvoiceSequence(9));

        await Update(12, "X-{NUMBER}");

        var sequence = _stored.ShouldHaveSingleItem();
        sequence.CurrentValue.ShouldBe(12);
        sequence.NumberFormat.ShouldBe("X-{NUMBER}");
        _sequences.Verify(r => r.UpdateAsync(sequence, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Update_BlankPattern_GoesBackToTheDefault()
    {
        var sequence = new InvoiceSequence(3);
        sequence.SetNumberFormat("X-{NUMBER}");
        _stored.Add(sequence);

        var numbering = await Update(3, "  ");

        sequence.NumberFormat.ShouldBeNull();
        numbering.CustomNumberFormat.ShouldBeNull();
        numbering.NumberFormat.ShouldBe("INV-{NUMBER:000}");
    }

    [TestCase(0)]
    [TestCase(-3)]
    public async Task Update_ValueBelowOne_Throws(int value)
    {
        await Should.ThrowAsync<ArgumentException>(() => Update(value, null));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Update_PatternWithoutTokens_ThrowsAndSavesNothing()
    {
        await Should.ThrowAsync<ArgumentException>(() => Update(1, "just-text"));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _drafts.Verify(d => d.RefreshDraftsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
