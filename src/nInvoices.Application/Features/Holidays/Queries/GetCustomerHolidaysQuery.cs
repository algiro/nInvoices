using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Holidays.Queries;

/// <summary>
/// The public holidays of the customer's holiday country in a month, or in the whole year when
/// <paramref name="Month"/> is null. Null when the customer doesn't exist.
/// </summary>
public sealed record GetCustomerHolidaysQuery(long CustomerId, int Year, int? Month) : IRequest<CustomerHolidaysDto?>;
