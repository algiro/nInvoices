using MediatR;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Handles invoice template deletion.
/// </summary>
public sealed class DeleteInvoiceTemplateCommandHandler : IRequestHandler<DeleteInvoiceTemplateCommand, bool>
{
    private readonly IRepository<InvoiceTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteInvoiceTemplateCommandHandler(IRepository<InvoiceTemplate> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteInvoiceTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template == null)
            return false;

        await _repository.DeleteAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}