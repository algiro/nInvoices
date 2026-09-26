using MediatR;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Handles invoice template deactivation.
/// </summary>
public sealed class DeactivateInvoiceTemplateCommandHandler : IRequestHandler<DeactivateInvoiceTemplateCommand, bool>
{
    private readonly IRepository<InvoiceTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateInvoiceTemplateCommandHandler(IRepository<InvoiceTemplate> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeactivateInvoiceTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template == null)
            return false;

        template.Deactivate();
        await _repository.UpdateAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
