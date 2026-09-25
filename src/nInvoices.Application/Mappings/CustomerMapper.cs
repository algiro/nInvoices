using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Mappings;

public static class CustomerMapper
{
    public static CustomerDto ToDto(Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.FiscalId,
        customer.Locale,
        new AddressDto(
            customer.Address.Street,
            customer.Address.HouseNumber,
            customer.Address.City,
            customer.Address.ZipCode,
            customer.Address.Country,
            customer.Address.State),
        customer.CreatedAt,
        customer.UpdatedAt ?? customer.CreatedAt,
        customer.Email,
        customer.CcEmails);
}
