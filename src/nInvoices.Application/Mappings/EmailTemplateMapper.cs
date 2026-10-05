using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Mappings;

/// <summary>
/// Maps <see cref="EmailTemplate"/> entities to DTOs.
/// </summary>
public static class EmailTemplateMapper
{
    public static EmailTemplateDto ToDto(EmailTemplate template) =>
        new(
            template.Id,
            template.CustomerId,
            template.Name,
            template.Subject,
            template.Body,
            template.IsActive,
            template.CreatedAt,
            template.UpdatedAt);
}
