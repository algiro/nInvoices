using nInvoices.Application.DTOs;
using nInvoices.Application.Localization;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Exceptions;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Services.InvoiceGeneration;

/// <summary>The rates an invoice is billed with, loaded from the customer's rates.</summary>
public interface IInvoiceRateResolver
{
    /// <summary>
    /// The invoice's default rate (the one chosen, else the customer's Daily, then Monthly, then
    /// Hourly rate) and the rates the worked days chose for themselves.
    /// </summary>
    /// <exception cref="DomainException">No rate applies, or a chosen rate is not the customer's.</exception>
    Task<DayRates> ResolveAsync(
        long customerId,
        InvoiceType invoiceType,
        long? rateId,
        IEnumerable<WorkDayDto>? workDays,
        CancellationToken cancellationToken = default);
}

public sealed class InvoiceRateResolver : IInvoiceRateResolver
{
    // Without a chosen rate: daily rate × worked days, then fixed monthly billing, then hourly rate × hours
    private static readonly RateType[] Preference = [RateType.Daily, RateType.Monthly, RateType.Hourly];

    private readonly IRepository<Rate> _rateRepository;
    private readonly IAppText _text;

    public InvoiceRateResolver(IRepository<Rate> rateRepository, IAppText text)
    {
        _rateRepository = rateRepository;
        _text = text;
    }

    public async Task<DayRates> ResolveAsync(
        long customerId,
        InvoiceType invoiceType,
        long? rateId,
        IEnumerable<WorkDayDto>? workDays,
        CancellationToken cancellationToken = default)
    {
        var defaultRate = await DefaultRateAsync(customerId, invoiceType, rateId, cancellationToken);

        // Only the days of a monthly invoice can choose their own rate
        var ids = DayRates.RequestedIds(invoiceType == InvoiceType.Monthly ? workDays : null, defaultRate);
        if (ids.Count == 0)
            return new DayRates(defaultRate, [], _text);

        var found = (await _rateRepository.FindAsync(
            r => r.CustomerId == customerId && ids.Contains(r.Id),
            cancellationToken)).ToList();

        var missing = ids.Except(found.Select(r => r.Id)).ToList();
        if (missing.Count > 0)
            throw new DomainException(_text.Translate("errors.rate.notFoundForCustomer", ("id", missing[0]), ("customer", customerId)));

        return new DayRates(defaultRate, found, _text);
    }

    private async Task<Rate> DefaultRateAsync(
        long customerId,
        InvoiceType invoiceType,
        long? rateId,
        CancellationToken cancellationToken)
    {
        if (invoiceType is not (InvoiceType.Monthly or InvoiceType.OneTime))
            throw new ArgumentException($"Unsupported invoice type: {invoiceType}", nameof(invoiceType));

        // The rate chosen for the invoice, which must be one of the customer's
        if (rateId.HasValue)
        {
            var chosen = (await _rateRepository.FindAsync(
                r => r.Id == rateId.Value && r.CustomerId == customerId,
                cancellationToken)).FirstOrDefault();

            return chosen
                ?? throw new DomainException(_text.Translate("errors.rate.notFoundForCustomer", ("id", rateId.Value), ("customer", customerId)));
        }

        foreach (var type in Preference)
        {
            var rate = (await _rateRepository.FindAsync(
                r => r.CustomerId == customerId && r.Type == type,
                cancellationToken)).FirstOrDefault();
            if (rate is not null)
                return rate;
        }

        throw new DomainException(_text.Translate("errors.rate.noActiveRate", ("customer", customerId)));
    }
}
