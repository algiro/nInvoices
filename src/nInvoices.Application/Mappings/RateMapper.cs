using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Mappings;

public static class RateMapper
{
    public static RateDto ToDto(Rate rate) => new(
        rate.Id,
        rate.CustomerId,
        rate.Type,
        new MoneyDto(rate.Price.Amount, rate.Price.Currency),
        rate.CreatedAt,
        rate.UpdatedAt ?? rate.CreatedAt,
        rate.Name);
}
