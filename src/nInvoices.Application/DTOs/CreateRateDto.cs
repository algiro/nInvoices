using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for creating a new rate.
/// </summary>
public sealed record CreateRateDto(
    long CustomerId,
    RateType Type,
    MoneyDto Price);
