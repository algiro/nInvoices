using Mediator;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed class DeleteInvoiceCommandHandler : IRequestHandler<DeleteInvoiceCommand, Unit>
{
    private readonly IRepository<Invoice> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReadOnlyList<IInvoiceLifecycleStep> _steps;

    public DeleteInvoiceCommandHandler(
        IRepository<Invoice> repository,
        IUnitOfWork unitOfWork,
        IEnumerable<IInvoiceLifecycleStep>? steps = null)
    {
        _steps = steps?.ToList() ?? [];
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<Unit> Handle(DeleteInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice == null)
            throw new InvalidOperationException($"Invoice with ID {request.InvoiceId} not found");

        // Check status only if not forcing delete
        if (!request.Force && invoice.Status != Core.Enums.InvoiceStatus.Draft)
            throw new InvalidOperationException("Only draft invoices can be deleted. Use force=true to delete finalized invoices.");

        // A step may refuse: an invoice that is part of a Verifactu chain cannot disappear
        foreach (var step in _steps)
            await step.OnDeletingAsync(invoice, cancellationToken);

        await _repository.DeleteAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
