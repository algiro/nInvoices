using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for tax information.
/// </summary>
public sealed record TaxDto(
    long Id,
    long CustomerId,
    string TaxId,
    string Description,
    string HandlerId,
    decimal Rate,
    TaxApplicationType ApplicationType,
    long? AppliedToTaxId,
    int Order,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);