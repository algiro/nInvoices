using Mediator;
using Microsoft.Extensions.Logging;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.MonthlyReportTemplates.Commands;

/// <summary>
/// Updates a monthly report template. The syntax is checked before this runs
/// (<c>UpdateMonthlyReportTemplateCommandValidator</c>).
/// </summary>
public sealed class UpdateMonthlyReportTemplateCommandHandler : IRequestHandler<UpdateMonthlyReportTemplateCommand, MonthlyReportTemplateDto?>
{
    private readonly IRepository<MonthlyReportTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateMonthlyReportTemplateCommandHandler> _logger;

    public UpdateMonthlyReportTemplateCommandHandler(
        IRepository<MonthlyReportTemplate> repository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateMonthlyReportTemplateCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async ValueTask<MonthlyReportTemplateDto?> Handle(UpdateMonthlyReportTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
            return null;

        template.Update(request.Template.Name, request.Template.Content);
        await _repository.UpdateAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Monthly report template {TemplateId} updated", template.Id);

        return MonthlyReportTemplateMapper.ToDto(template);
    }
}
