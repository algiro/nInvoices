using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Localization;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.BusinessExpenses;

/// <summary>The business expenses dated in <paramref name="Year"/>, oldest first.</summary>
public sealed record GetBusinessExpensesQuery(int Year) : IRequest<IReadOnlyList<BusinessExpenseDto>>;

public sealed record CreateBusinessExpenseCommand(SaveBusinessExpenseDto Expense) : IRequest<BusinessExpenseDto>;

public sealed record UpdateBusinessExpenseCommand(long Id, SaveBusinessExpenseDto Expense) : IRequest<BusinessExpenseDto>;

/// <summary>Result: false if there is no such expense.</summary>
public sealed record DeleteBusinessExpenseCommand(long Id) : IRequest<bool>;

public sealed class GetBusinessExpensesQueryHandler : IRequestHandler<GetBusinessExpensesQuery, IReadOnlyList<BusinessExpenseDto>>
{
    private readonly IRepository<BusinessExpense> _expenses;

    public GetBusinessExpensesQueryHandler(IRepository<BusinessExpense> expenses)
    {
        _expenses = expenses;
    }

    public async ValueTask<IReadOnlyList<BusinessExpenseDto>> Handle(GetBusinessExpensesQuery request, CancellationToken cancellationToken)
    {
        var from = new DateOnly(request.Year, 1, 1);
        var to = new DateOnly(request.Year, 12, 31);

        // The date is stored in clear: filtered in SQL, the rest is encrypted
        var expenses = await _expenses.FindAsync(e => e.Date >= from && e.Date <= to, cancellationToken);
        return expenses.OrderBy(e => e.Date).ThenBy(e => e.Id).Select(BusinessExpenseMapper.ToDto).ToList();
    }
}

public sealed class CreateBusinessExpenseCommandHandler : IRequestHandler<CreateBusinessExpenseCommand, BusinessExpenseDto>
{
    private readonly IRepository<BusinessExpense> _expenses;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBusinessExpenseCommandHandler(IRepository<BusinessExpense> expenses, IUnitOfWork unitOfWork)
    {
        _expenses = expenses;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<BusinessExpenseDto> Handle(CreateBusinessExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = BusinessExpenseMapper.ToEntity(request.Expense);

        await _expenses.AddAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return BusinessExpenseMapper.ToDto(expense);
    }
}

public sealed class UpdateBusinessExpenseCommandHandler : IRequestHandler<UpdateBusinessExpenseCommand, BusinessExpenseDto>
{
    private readonly IRepository<BusinessExpense> _expenses;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppText _text;

    public UpdateBusinessExpenseCommandHandler(IRepository<BusinessExpense> expenses, IUnitOfWork unitOfWork, IAppText text)
    {
        _expenses = expenses;
        _unitOfWork = unitOfWork;
        _text = text;
    }

    public async ValueTask<BusinessExpenseDto> Handle(UpdateBusinessExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await _expenses.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(_text.Translate("notFound.expense", ("id", request.Id)));

        BusinessExpenseMapper.Apply(request.Expense, expense);
        await _expenses.UpdateAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return BusinessExpenseMapper.ToDto(expense);
    }
}

public sealed class DeleteBusinessExpenseCommandHandler : IRequestHandler<DeleteBusinessExpenseCommand, bool>
{
    private readonly IRepository<BusinessExpense> _expenses;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteBusinessExpenseCommandHandler(IRepository<BusinessExpense> expenses, IUnitOfWork unitOfWork)
    {
        _expenses = expenses;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<bool> Handle(DeleteBusinessExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await _expenses.GetByIdAsync(request.Id, cancellationToken);
        if (expense is null)
            return false;

        await _expenses.DeleteAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
