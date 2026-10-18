using Mediator;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Localization;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Commands;

/// <summary>
/// Finalizes a draft. This is the moment it takes its number from the sequence; the other drafts
/// then move on to the next number.
/// </summary>
public sealed class FinalizeInvoiceCommandHandler : IRequestHandler<FinalizeInvoiceCommand, Unit>
{
    private readonly IRepository<Invoice> _repository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IInvoiceFinalizer _finalizer;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppText _text;

    public FinalizeInvoiceCommandHandler(
        IRepository<Invoice> repository,
        IRepository<Customer> customerRepository,
        IInvoiceFinalizer finalizer,
        IUnitOfWork unitOfWork,
        IAppText text)
    {
        _repository = repository;
        _customerRepository = customerRepository;
        _finalizer = finalizer;
        _unitOfWork = unitOfWork;
        _text = text;
    }

    public async ValueTask<Unit> Handle(FinalizeInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetByIdAsync(request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException(_text.Translate("notFound.invoice", ("id", request.InvoiceId)));

        var customer = await _customerRepository.GetByIdAsync(invoice.CustomerId, cancellationToken)
            ?? throw new NotFoundException(_text.Translate("notFound.customer", ("id", invoice.CustomerId)));

        var outdated = await _finalizer.FinalizeAsync(invoice, customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _finalizer.CompleteAsync([invoice.Id], outdated ? [invoice.Id] : [], cancellationToken);

        return Unit.Value;
    }
}
