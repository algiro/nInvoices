using Mediator;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Localization;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed class MarkAsPaidCommandHandler : IRequestHandler<MarkAsPaidCommand, Unit>
{
    private readonly IRepository<Invoice> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppText _text;

    public MarkAsPaidCommandHandler(IRepository<Invoice> repository, IUnitOfWork unitOfWork, IAppText text)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _text = text;
    }

    public async ValueTask<Unit> Handle(MarkAsPaidCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice == null)
            throw new NotFoundException(_text.Translate("notFound.invoice", ("id", request.InvoiceId)));

        invoice.MarkAsPaid();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
