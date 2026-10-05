using Mediator;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.MonthlyReportTemplates.Commands;

/// <summary>Changes a monthly report template's name and content; null when it doesn't exist.</summary>
public sealed record UpdateMonthlyReportTemplateCommand(long Id, UpdateMonthlyReportTemplateDto Template) : IRequest<MonthlyReportTemplateDto?>;
