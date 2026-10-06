using Mediator;
using Microsoft.Extensions.Logging;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.MonthlyReportTemplates.Commands;

/// <summary>Deletes a monthly report template; false when it doesn't exist.</summary>
public sealed record DeleteMonthlyReportTemplateCommand(long Id) : IRequest<bool>;

/// <summary>
/// Makes the template the active one of its customer (or of the shared ones) for its invoice type,
/// switching the previous one off; false when it doesn't exist.
/// </summary>
public sealed record ActivateMonthlyReportTemplateCommand(long Id) : IRequest<bool>;

/// <summary>Switches a monthly report template off; false when it doesn't exist.</summary>
public sealed record DeactivateMonthlyReportTemplateCommand(long Id) : IRequest<bool>;

public sealed class MonthlyReportTemplateLifecycleHandlers :
    IRequestHandler<DeleteMonthlyReportTemplateCommand, bool>,
    IRequestHandler<ActivateMonthlyReportTemplateCommand, bool>,
    IRequestHandler<DeactivateMonthlyReportTemplateCommand, bool>
{
    private readonly IRepository<MonthlyReportTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MonthlyReportTemplateLifecycleHandlers> _logger;

    public MonthlyReportTemplateLifecycleHandlers(
        IRepository<MonthlyReportTemplate> repository,
        IUnitOfWork unitOfWork,
        ILogger<MonthlyReportTemplateLifecycleHandlers> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async ValueTask<bool> Handle(DeleteMonthlyReportTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
            return false;

        await _repository.DeleteAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Monthly report template {TemplateId} deleted", request.Id);
        return true;
    }

    public async ValueTask<bool> Handle(ActivateMonthlyReportTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
            return false;

        // One active template per customer and type: the previous one is switched off
        var previouslyActive = await _repository.FindAsync(
            t => t.CustomerId == template.CustomerId && t.InvoiceType == template.InvoiceType && t.IsActive && t.Id != request.Id,
            cancellationToken);
        foreach (var other in previouslyActive)
        {
            other.Deactivate();
            await _repository.UpdateAsync(other, cancellationToken);
        }

        template.Activate();
        await _repository.UpdateAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Monthly report template {TemplateId} activated", request.Id);
        return true;
    }

    public async ValueTask<bool> Handle(DeactivateMonthlyReportTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
            return false;

        template.Deactivate();
        await _repository.UpdateAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Monthly report template {TemplateId} deactivated", request.Id);
        return true;
    }
}
