using Mediator;
using nInvoices.Application.Localization;
using nInvoices.Core.Entities;
using nInvoices.Core.Exceptions;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Customers.Commands;

public sealed class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand, bool>
{
    private readonly IRepository<Customer> _repository;
    private readonly IWorkDayRepository _workDays;
    private readonly IInvoiceRepository _invoices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppText _text;

    public DeleteCustomerCommandHandler(
        IRepository<Customer> repository,
        IWorkDayRepository workDays,
        IInvoiceRepository invoices,
        IUnitOfWork unitOfWork,
        IAppText text)
    {
        _repository = repository;
        _workDays = workDays;
        _invoices = invoices;
        _unitOfWork = unitOfWork;
        _text = text;
    }

    public async ValueTask<bool> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (customer == null)
            return false;

        // An invoice keeps its customer on record: it is cancelled, not orphaned
        var invoices = await _invoices.FindAsync(i => i.CustomerId == request.Id, cancellationToken);
        if (invoices.Any())
            throw new DomainException(_text.Translate("errors.customer.hasInvoices"));

        // The days go first: they point at the customer's projects, and a project that a day still
        // points at can't be removed along with the customer
        var workDays = await _workDays.FindAsync(w => w.CustomerId == request.Id, cancellationToken);
        foreach (var workDay in workDays)
            await _workDays.DeleteAsync(workDay, cancellationToken);

        await _repository.DeleteAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
