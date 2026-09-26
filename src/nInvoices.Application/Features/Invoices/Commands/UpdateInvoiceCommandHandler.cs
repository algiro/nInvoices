using MediatR;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed class UpdateInvoiceCommandHandler : IRequestHandler<UpdateInvoiceCommand, Unit>
{
    private readonly IRepository<Invoice> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateInvoiceCommandHandler(
        IRepository<Invoice> repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(UpdateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice == null)
            throw new InvalidOperationException($"Invoice with ID {request.InvoiceId} not found");

        if (invoice.Status != Core.Enums.InvoiceStatus.Draft)
            throw new InvalidOperationException("Only draft invoices can be updated");

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
