using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for creating a new tax.
/// </summary>
public sealed record CreateTaxDto(
    long CustomerId,
    string TaxId,
    string Description,
    string HandlerId,
    decimal Rate,
    TaxApplicationType ApplicationType,
    long? AppliedToTaxId = null,
    int Order = 0);