namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for customer information.
/// </summary>
public sealed record CustomerDto(
    long Id,
    string Name,
    string FiscalId,
    AddressDto Address,
    DateTime CreatedAt,
    DateTime UpdatedAt);
