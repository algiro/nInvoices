using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Localization;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.BusinessExpenses;

/// <summary>Every recurring expense, by the month it starts.</summary>
public sealed record GetRecurringExpensesQuery : IRequest<IReadOnlyList<RecurringExpenseDto>>;

public sealed record CreateRecurringExpenseCommand(SaveRecurringExpenseDto Expense) : IRequest<RecurringExpenseDto>;

public sealed record UpdateRecurringExpenseCommand(long Id, SaveRecurringExpenseDto Expense) : IRequest<RecurringExpenseDto>;

/// <summary>Result: false if there is no such expense.</summary>
public sealed record DeleteRecurringExpenseCommand(long Id) : IRequest<bool>;

public sealed class GetRecurringExpensesQueryHandler : IRequestHandler<GetRecurringExpensesQuery, IReadOnlyList<RecurringExpenseDto>>
{
    private readonly IRepository<RecurringExpense> _expenses;

    public GetRecurringExpensesQueryHandler(IRepository<RecurringExpense> expenses)
    {
        _expenses = expenses;
    }

    public async ValueTask<IReadOnlyList<RecurringExpenseDto>> Handle(GetRecurringExpensesQuery request, CancellationToken cancellationToken) =>
        (await _expenses.GetAllAsync(cancellationToken))
            .OrderBy(e => e.FromMonth)
            .ThenBy(e => e.Id)
            .Select(BusinessExpenseMapper.ToDto)
            .ToList();
}

public sealed class CreateRecurringExpenseCommandHandler : IRequestHandler<CreateRecurringExpenseCommand, RecurringExpenseDto>
{
    private readonly IRepository<RecurringExpense> _expenses;
    private readonly IUnitOfWork _unitOfWork;

    public CreateRecurringExpenseCommandHandler(IRepository<RecurringExpense> expenses, IUnitOfWork unitOfWork)
    {
        _expenses = expenses;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<RecurringExpenseDto> Handle(CreateRecurringExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = BusinessExpenseMapper.ToEntity(request.Expense);

        await _expenses.AddAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return BusinessExpenseMapper.ToDto(expense);
    }
}

public sealed class UpdateRecurringExpenseCommandHandler : IRequestHandler<UpdateRecurringExpenseCommand, RecurringExpenseDto>
{
    private readonly IRepository<RecurringExpense> _expenses;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppText _text;

    public UpdateRecurringExpenseCommandHandler(IRepository<RecurringExpense> expenses, IUnitOfWork unitOfWork, IAppText text)
    {
        _expenses = expenses;
        _unitOfWork = unitOfWork;
        _text = text;
    }

    public async ValueTask<RecurringExpenseDto> Handle(UpdateRecurringExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await _expenses.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(_text.Translate("notFound.recurringExpense", ("id", request.Id)));

        BusinessExpenseMapper.Apply(request.Expense, expense);
        await _expenses.UpdateAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return BusinessExpenseMapper.ToDto(expense);
    }
}

public sealed class DeleteRecurringExpenseCommandHandler : IRequestHandler<DeleteRecurringExpenseCommand, bool>
{
    private readonly IRepository<RecurringExpense> _expenses;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteRecurringExpenseCommandHandler(IRepository<RecurringExpense> expenses, IUnitOfWork unitOfWork)
    {
        _expenses = expenses;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<bool> Handle(DeleteRecurringExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await _expenses.GetByIdAsync(request.Id, cancellationToken);
        if (expense is null)
            return false;

        await _expenses.DeleteAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
