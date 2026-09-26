using MediatR;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Handles invoice template activation.
/// </summary>
public sealed class ActivateInvoiceTemplateCommandHandler : IRequestHandler<ActivateInvoiceTemplateCommand, bool>
{
    private readonly IRepository<InvoiceTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateInvoiceTemplateCommandHandler(IRepository<InvoiceTemplate> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(ActivateInvoiceTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template == null)
            return false;

        // Also deactivates the template that was active for this customer and type
        await InvoiceTemplateActivation.ActivateAsync(template, _repository, _unitOfWork, cancellationToken);

        return true;
    }
}
