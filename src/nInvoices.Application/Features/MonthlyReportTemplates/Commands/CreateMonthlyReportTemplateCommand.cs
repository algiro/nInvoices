using Mediator;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.MonthlyReportTemplates.Commands;

/// <summary>Creates a monthly report (timesheet) template, for a customer or shared (no customer).</summary>
public sealed record CreateMonthlyReportTemplateCommand(CreateMonthlyReportTemplateDto Template) : IRequest<MonthlyReportTemplateDto>;
