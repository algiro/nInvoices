using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for updating an existing tax.
/// </summary>
public sealed record UpdateTaxDto(
    string Description,
    string HandlerId,
    decimal Rate,
    TaxApplicationType ApplicationType,
    long? AppliedToTaxId = null,
    int Order = 0,
    bool IsActive = true);