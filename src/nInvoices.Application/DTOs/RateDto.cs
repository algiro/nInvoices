using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for rate information.
/// </summary>
public sealed record RateDto(
    long Id,
    long CustomerId,
    RateType Type,
    MoneyDto Price,
    DateTime CreatedAt,
    DateTime UpdatedAt);
