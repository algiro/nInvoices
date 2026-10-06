using Mediator;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Application.Exceptions;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed class MarkAsPaidCommandHandler : IRequestHandler<MarkAsPaidCommand, Unit>
{
    private readonly IRepository<Invoice> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public MarkAsPaidCommandHandler(IRepository<Invoice> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<Unit> Handle(MarkAsPaidCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice == null)
            throw new NotFoundException($"Invoice with ID {request.InvoiceId} not found");

        invoice.MarkAsPaid();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
