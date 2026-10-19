using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.TaxPrediction;

namespace nInvoices.Application.Mappings;

/// <summary>Maps the tax prediction (modules, profile, estimate, payments) to DTOs.</summary>
public static class TaxPredictionMapper
{
    public static TaxCountryDto ToDto(ITaxPredictionModule module) => new(
        module.CountryCode,
        module.DisplayName,
        module.Currency,
        module.IndirectTaxName,
        module.Regimes.Select(r => new TaxRegimeDto(
            r.Id,
            r.Name,
            r.Help,
            r.Fields.Select(ComplianceMapper.ToDto).ToList(),
            r.Defaults ?? new Dictionary<string, string>())).ToList());

    public static TaxProfileDto ToDto(TaxProfile profile) =>
        new(profile.CountryCode, profile.RegimeId, profile.Values);

    public static TaxPaymentDto ToDto(TaxPayment payment) =>
        new(payment.Id, payment.Year, payment.Period, payment.Form, payment.Amount.Amount, payment.Amount.Currency, payment.PaidOn);

    /// <param name="payments">The recorded payments of the year, shown with their form.</param>
    /// <param name="warnings">Added before the module's own.</param>
    public static TaxPredictionDto ToDto(
        ITaxPredictionModule module,
        TaxYear taxYear,
        TaxYearPrediction prediction,
        IReadOnlyList<TaxPayment> payments,
        IReadOnlyList<string> warnings) => new(
        prediction.Year,
        IsSetUp: true,
        module.CountryCode,
        module.Currency,
        prediction.Periods.Select(p => new TaxPeriodDto(
            p.Period,
            p.Name,
            p.Start,
            p.End,
            p.Forms.Select(f => new TaxFormDto(
                f.Code,
                f.Name,
                f.Deadline,
                f.Estimated,
                payments.FirstOrDefault(x => x.Period == p.Period && x.Form == f.Code) is { } payment ? ToDto(payment) : null,
                f.Lines.Select(l => new TaxFormLineDto(l.Box, l.Label, l.Amount)).ToList())).ToList())).ToList(),
        [.. warnings, .. prediction.Warnings],
        taxYear.Label,
        taxYear.Start,
        taxYear.End);

    /// <summary>The estimate of a user who hasn't said where they pay taxes, or whose country is no longer offered.</summary>
    public static TaxPredictionDto NotSetUp(int year, string reason) =>
        new(year, IsSetUp: false, null, null, [], [reason]);
}
