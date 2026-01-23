namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for address information.
/// </summary>
public sealed record AddressDto(
    string Street,
    string HouseNumber,
    string City,
    string ZipCode,
    string Country,
    string? State = null);
