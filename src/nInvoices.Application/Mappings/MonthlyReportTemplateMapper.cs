using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Mappings;

/// <summary>
/// Maps <see cref="MonthlyReportTemplate"/> entities to DTOs.
/// </summary>
public static class MonthlyReportTemplateMapper
{
    public static MonthlyReportTemplateDto ToDto(MonthlyReportTemplate template) =>
        new(
            template.Id,
            template.CustomerId,
            template.InvoiceType,
            template.Name,
            template.Content,
            template.IsActive,
            template.CreatedAt,
            template.UpdatedAt);
}
