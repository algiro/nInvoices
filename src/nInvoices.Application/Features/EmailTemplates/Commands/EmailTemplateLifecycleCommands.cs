using Mediator;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.EmailTemplates.Commands;

/// <summary>Deletes an email template; false when it doesn't exist.</summary>
public sealed record DeleteEmailTemplateCommand(long Id) : IRequest<bool>;

/// <summary>
/// Makes the template the active one of its customer (or of the shared ones), switching the others
/// off; false when it doesn't exist. The active template is preselected when composing an email.
/// </summary>
public sealed record ActivateEmailTemplateCommand(long Id) : IRequest<bool>;

/// <summary>Switches an email template off; false when it doesn't exist.</summary>
public sealed record DeactivateEmailTemplateCommand(long Id) : IRequest<bool>;

public sealed class EmailTemplateLifecycleHandlers :
    IRequestHandler<DeleteEmailTemplateCommand, bool>,
    IRequestHandler<ActivateEmailTemplateCommand, bool>,
    IRequestHandler<DeactivateEmailTemplateCommand, bool>
{
    private readonly IRepository<EmailTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public EmailTemplateLifecycleHandlers(IRepository<EmailTemplate> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<bool> Handle(DeleteEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
            return false;

        await _repository.DeleteAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async ValueTask<bool> Handle(ActivateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
            return false;

        var others = await _repository.FindAsync(t => t.CustomerId == template.CustomerId && t.IsActive && t.Id != request.Id, cancellationToken);
        foreach (var other in others)
            other.Deactivate();
        template.Activate();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async ValueTask<bool> Handle(DeactivateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
            return false;

        template.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
