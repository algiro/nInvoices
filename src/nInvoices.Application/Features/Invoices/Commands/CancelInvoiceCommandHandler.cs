using MediatR;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed class CancelInvoiceCommandHandler : IRequestHandler<CancelInvoiceCommand, Unit>
{
    private readonly IRepository<Invoice> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReadOnlyList<IInvoiceLifecycleStep> _steps;

    public CancelInvoiceCommandHandler(
        IRepository<Invoice> repository, IUnitOfWork unitOfWork, IEnumerable<IInvoiceLifecycleStep>? steps = null)
    {
        _steps = steps?.ToList() ?? [];
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(CancelInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice == null)
            throw new InvalidOperationException($"Invoice with ID {request.InvoiceId} not found");

        invoice.Cancel();

        foreach (var step in _steps)
            await step.OnCancellingAsync(invoice, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
