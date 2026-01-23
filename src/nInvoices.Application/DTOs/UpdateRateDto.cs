using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for updating an existing rate.
/// </summary>
public sealed record UpdateRateDto(
    RateType Type,
    MoneyDto Price);
