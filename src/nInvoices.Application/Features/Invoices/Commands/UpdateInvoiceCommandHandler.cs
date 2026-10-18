using Mediator;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Localization;
using nInvoices.Core.Entities;
using nInvoices.Core.Exceptions;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed class UpdateInvoiceCommandHandler : IRequestHandler<UpdateInvoiceCommand, Unit>
{
    private readonly IRepository<Invoice> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppText _text;

    public UpdateInvoiceCommandHandler(
        IRepository<Invoice> repository,
        IUnitOfWork unitOfWork,
        IAppText text)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _text = text;
    }

    public async ValueTask<Unit> Handle(UpdateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice == null)
            throw new NotFoundException(_text.Translate("notFound.invoice", ("id", request.InvoiceId)));

        if (invoice.Status != Core.Enums.InvoiceStatus.Draft)
            throw new DomainException(_text.Translate("errors.invoice.onlyDraftUpdated"));

        if (request.Dto.DueDate.HasValue)
        {
            invoice.DueDate = request.Dto.DueDate.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Dto.Notes))
        {
            invoice.Notes = request.Dto.Notes;
        }

        if (!string.IsNullOrWhiteSpace(request.Dto.RenderedContent))
        {
            invoice.SetRenderedContent(request.Dto.RenderedContent);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
