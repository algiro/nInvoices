using Mediator;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Localization;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed class DeleteInvoiceCommandHandler : IRequestHandler<DeleteInvoiceCommand, Unit>
{
    private readonly IRepository<Invoice> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReadOnlyList<IInvoiceLifecycleStep> _steps;
    private readonly IAppText _text;

    public DeleteInvoiceCommandHandler(
        IRepository<Invoice> repository,
        IUnitOfWork unitOfWork,
        IAppText text,
        IEnumerable<IInvoiceLifecycleStep>? steps = null)
    {
        _steps = steps?.ToList() ?? [];
        _repository = repository;
        _unitOfWork = unitOfWork;
        _text = text;
    }

    public async ValueTask<Unit> Handle(DeleteInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice == null)
            throw new NotFoundException(_text.Translate("notFound.invoice", ("id", request.InvoiceId)));

        // Only a draft can be deleted (InvoiceLifecycle); a forced delete, asked for explicitly, bypasses it
        if (!request.Force)
            invoice.EnsureAllowed(InvoiceAction.Delete);

        // A step may refuse: an invoice that is part of a Verifactu chain cannot disappear
        foreach (var step in _steps)
            await step.OnDeletingAsync(invoice, cancellationToken);

        await _repository.DeleteAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
