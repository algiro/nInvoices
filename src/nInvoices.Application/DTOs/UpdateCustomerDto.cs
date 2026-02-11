namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for updating an existing customer.
/// </summary>
public sealed record UpdateCustomerDto(
    string Name,
    string FiscalId,
    AddressDto Address,
    string Locale = "en-US");
