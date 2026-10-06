using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.MonthlyReportTemplates.Queries;

/// <summary>The monthly report templates of a customer, or the shared ones when <paramref name="CustomerId"/> is null.</summary>
public sealed record GetMonthlyReportTemplatesQuery(long? CustomerId) : IRequest<IReadOnlyList<MonthlyReportTemplateDto>>;

/// <summary>A monthly report template; null when it doesn't exist.</summary>
public sealed record GetMonthlyReportTemplateByIdQuery(long Id) : IRequest<MonthlyReportTemplateDto?>;

public sealed class MonthlyReportTemplateQueryHandlers :
    IRequestHandler<GetMonthlyReportTemplatesQuery, IReadOnlyList<MonthlyReportTemplateDto>>,
    IRequestHandler<GetMonthlyReportTemplateByIdQuery, MonthlyReportTemplateDto?>
{
    private readonly IRepository<MonthlyReportTemplate> _repository;

    public MonthlyReportTemplateQueryHandlers(IRepository<MonthlyReportTemplate> repository)
    {
        _repository = repository;
    }

    public async ValueTask<IReadOnlyList<MonthlyReportTemplateDto>> Handle(GetMonthlyReportTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await _repository.FindAsync(t => t.CustomerId == request.CustomerId, cancellationToken);
        return templates.Select(MonthlyReportTemplateMapper.ToDto).ToList();
    }

    public async ValueTask<MonthlyReportTemplateDto?> Handle(GetMonthlyReportTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return template is null ? null : MonthlyReportTemplateMapper.ToDto(template);
    }
}
