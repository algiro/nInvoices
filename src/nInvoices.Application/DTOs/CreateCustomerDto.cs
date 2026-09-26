namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for creating a new customer.
/// </summary>
public sealed record CreateCustomerDto(
    string Name,
    string FiscalId,
    AddressDto Address,
    string Locale = "en-US",
    string? Email = null,
    string? CcEmails = null,
    string? HolidayCountry = null);
