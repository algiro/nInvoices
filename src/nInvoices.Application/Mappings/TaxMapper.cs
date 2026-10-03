using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Mappings;

public static class TaxMapper
{
    public static TaxDto ToDto(Tax tax) => new(
        tax.Id,
        tax.CustomerId,
        tax.TaxId,
        tax.Description,
        tax.HandlerId,
        tax.Rate,
        tax.ApplicationType,
        tax.AppliedToTaxId,
        tax.Order,
        tax.IsActive,
        tax.CreatedAt,
        tax.UpdatedAt ?? tax.CreatedAt,
        tax.ComplianceValues);
}
