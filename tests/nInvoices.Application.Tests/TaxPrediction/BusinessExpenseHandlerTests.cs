using FluentValidation.TestHelper;
using Moq;
using nInvoices.Application.DTOs;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Features.BusinessExpenses;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Application.Tests.TaxPrediction;

/// <summary>Business and recurring expenses: their requests and validation.</summary>
[TestFixture]
public sealed class BusinessExpenseHandlerTests
{
    private InMemoryRepository<BusinessExpense> _expenses = null!;
    private InMemoryRepository<RecurringExpense> _recurring = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public void SetUp()
    {
        _expenses = new InMemoryRepository<BusinessExpense>();
        _recurring = new InMemoryRepository<RecurringExpense>();
        _unitOfWork = new Mock<IUnitOfWork>();
    }

    private static SaveBusinessExpenseDto Laptop(decimal amount = 1000m) =>
        new(new DateOnly(2026, 3, 2), "Laptop", "Shop", new MoneyDto(amount, "EUR"), 70m, Category: ExpenseCategory.Equipment, DeductiblePercent: 50m);

    [Test]
    public async Task Create_ThenUpdate_KeepsEveryField()
    {
        var created = await new CreateBusinessExpenseCommandHandler(_expenses, _unitOfWork.Object)
            .Handle(new CreateBusinessExpenseCommand(Laptop()), Token);
        var updated = await new UpdateBusinessExpenseCommandHandler(_expenses, _unitOfWork.Object, TestText.Instance)
            .Handle(new UpdateBusinessExpenseCommand(created.Id, Laptop(1200m) with { Supplier = null }), Token);

        updated.ShouldBe(new BusinessExpenseDto(created.Id, new DateOnly(2026, 3, 2), "Laptop", null, new MoneyDto(1200m, "EUR"),
            70m, false, ExpenseCategory.Equipment, 50m));
        _expenses.Items.ShouldHaveSingleItem();
    }

    [Test]
    public async Task Update_Unknown_IsNotFound()
    {
        await Should.ThrowAsync<NotFoundException>(() => new UpdateBusinessExpenseCommandHandler(_expenses, _unitOfWork.Object, TestText.Instance)
            .Handle(new UpdateBusinessExpenseCommand(99, Laptop()), Token).AsTask());
    }

    [Test]
    public async Task GetByYear_ListsThatYearOnly_OldestFirst()
    {
        _expenses.Items.AddRange(
        [
            new BusinessExpense(new DateOnly(2026, 5, 1), "May", new Money(1m, "EUR")) { Id = 1 },
            new BusinessExpense(new DateOnly(2025, 12, 31), "Last year", new Money(1m, "EUR")) { Id = 2 },
            new BusinessExpense(new DateOnly(2026, 1, 1), "January", new Money(1m, "EUR")) { Id = 3 }
        ]);

        var list = await new GetBusinessExpensesQueryHandler(_expenses).Handle(new GetBusinessExpensesQuery(2026), Token);

        list.Select(e => e.Description).ShouldBe(["January", "May"]);
    }

    [Test]
    public async Task Delete_RemovesIt_OrSaysItWasNotThere()
    {
        _expenses.Items.Add(new BusinessExpense(new DateOnly(2026, 5, 1), "May", new Money(1m, "EUR")) { Id = 4 });
        var handler = new DeleteBusinessExpenseCommandHandler(_expenses, _unitOfWork.Object);

        (await handler.Handle(new DeleteBusinessExpenseCommand(4), Token)).ShouldBeTrue();
        (await handler.Handle(new DeleteBusinessExpenseCommand(4), Token)).ShouldBeFalse();
    }

    [Test]
    public async Task Recurring_CreateThenUpdate_StoresWholeMonths()
    {
        var created = await new CreateRecurringExpenseCommandHandler(_recurring, _unitOfWork.Object)
            .Handle(new CreateRecurringExpenseCommand(new SaveRecurringExpenseDto(
                "Social Security", new MoneyDto(300.00m, "EUR"), new DateOnly(2026, 1, 15), new DateOnly(2026, 6, 30), ExpenseCategory.SocialSecurity)), Token);
        var updated = await new UpdateRecurringExpenseCommandHandler(_recurring, _unitOfWork.Object, TestText.Instance)
            .Handle(new UpdateRecurringExpenseCommand(created.Id, new SaveRecurringExpenseDto(
                "Social Security", new MoneyDto(500.00m, "EUR"), new DateOnly(2026, 7, 1), Category: ExpenseCategory.SocialSecurity)), Token);

        (created.FromMonth, created.ToMonth).ShouldBe((new DateOnly(2026, 1, 1), (DateOnly?)new DateOnly(2026, 6, 1)));
        (updated.Amount.Amount, updated.FromMonth, updated.ToMonth).ShouldBe((500.00m, new DateOnly(2026, 7, 1), (DateOnly?)null));
    }

    [Test]
    public void Validator_ReverseChargeWithTax_AndPercentOver100_AreRejected()
    {
        var result = new SaveBusinessExpenseDtoValidator(TestText.Instance).TestValidate(
            Laptop() with { ReverseCharge = true, DeductiblePercent = 120m, Description = "" });

        result.ShouldHaveValidationErrorFor(x => x.IndirectTax);
        result.ShouldHaveValidationErrorFor(x => x.DeductiblePercent);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Test]
    public void Validator_RecurringEndingTheMonthBeforeItStarts_IsRejected_TheSameMonthIsNot()
    {
        var validator = new SaveRecurringExpenseDtoValidator(TestText.Instance);
        var dto = new SaveRecurringExpenseDto("Fee", new MoneyDto(1m, "EUR"), new DateOnly(2026, 5, 20));

        validator.TestValidate(dto with { ToMonth = new DateOnly(2026, 4, 30) }).ShouldHaveValidationErrorFor(x => x.ToMonth);
        validator.TestValidate(dto with { ToMonth = new DateOnly(2026, 5, 1) }).ShouldNotHaveAnyValidationErrors();
    }
}
