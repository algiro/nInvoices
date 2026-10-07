using System.Globalization;
using Mediator;
using Microsoft.Extensions.Logging;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.MonthlyReportTemplates.Commands;

/// <summary>
/// Creates a monthly report template. The syntax is checked before this runs
/// (<c>CreateMonthlyReportTemplateCommandValidator</c>); the entity rejects a blank name or content.
/// </summary>
public sealed class CreateMonthlyReportTemplateCommandHandler : IRequestHandler<CreateMonthlyReportTemplateCommand, MonthlyReportTemplateDto>
{
    private readonly IRepository<MonthlyReportTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateMonthlyReportTemplateCommandHandler> _logger;

    public CreateMonthlyReportTemplateCommandHandler(
        IRepository<MonthlyReportTemplate> repository,
        IUnitOfWork unitOfWork,
        ILogger<CreateMonthlyReportTemplateCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async ValueTask<MonthlyReportTemplateDto> Handle(CreateMonthlyReportTemplateCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Template;
        var template = new MonthlyReportTemplate(dto.CustomerId, dto.Name, dto.Content, dto.InvoiceType);

        await _repository.AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Monthly report template {TemplateId} created for customer {CustomerId}",
            template.Id,
            template.CustomerId?.ToString(CultureInfo.InvariantCulture) ?? "(shared)");

        return MonthlyReportTemplateMapper.ToDto(template);
    }
}
