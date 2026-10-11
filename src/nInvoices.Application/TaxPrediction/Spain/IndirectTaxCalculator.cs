using nInvoices.Application.Compliance.Spain;
using nInvoices.Core.TaxPrediction;
using static nInvoices.Application.TaxPrediction.Spain.SpanishTaxRules;

namespace nInvoices.Application.TaxPrediction.Spain;

/// <summary>The quarterly indirect-tax return of a territory: IGIC in the Canary Islands, IVA elsewhere.</summary>
/// <param name="Kind">The tax it settles, as <see cref="ChargedTax.Kind"/> names it.</param>
/// <param name="GeneralRate">The rate a reverse-charge expense is self-charged at.</param>
public sealed record IndirectTaxReturn(string Code, string Name, string Kind, decimal GeneralRate)
{
    public static readonly IndirectTaxReturn Igic420 =
        new("420", "Modelo 420 · IGIC", SpainComplianceModule.Igic, IgicGeneralRate);

    public static readonly IndirectTaxReturn Iva303 =
        new("303", "Modelo 303 · IVA", SpainComplianceModule.Iva, IvaGeneralRate);
}

/// <summary>
/// Modelo 420 / 303: each quarter on its own, the tax charged on the invoices issued in it minus the
/// tax paid on its expenses. A negative result is not paid: it is carried to the next quarters. A tax
/// that doesn't say which it is counts as the return's own (IGIC in the Canary Islands, IVA elsewhere).
/// </summary>
public static class IndirectTaxCalculator
{
    /// <param name="paid">What the user recorded as paid, by quarter.</param>
    public static IReadOnlyList<TaxFormPrediction> Calculate(
        IndirectTaxReturn form,
        int year,
        IReadOnlyList<TaxableIncome> income,
        IReadOnlyList<DeductibleExpense> expenses,
        IReadOnlyDictionary<int, decimal> paid)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(income);
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(paid);

        var forms = new List<TaxFormPrediction>();
        var credit = 0m;

        for (var quarter = 1; quarter <= 4; quarter++)
        {
            var start = QuarterStart(year, quarter);
            var end = QuarterEnd(year, quarter);
            var costs = expenses.Where(e => e.Date >= start && e.Date <= end).ToList();

            var charged = Round(income
                .Where(i => i.Date >= start && i.Date <= end)
                .SelectMany(i => i.Taxes)
                .Where(t => t.Kind is null || t.Kind == form.Kind)
                .Sum(t => t.Amount));

            // A supplier abroad charged no tax: the user charges it to themselves and deducts it as well
            var selfCharged = Round(costs.Where(e => e.ReverseCharge).Sum(e => e.Base * form.GeneralRate / 100m));
            var deductible = Round(costs.Sum(e =>
                (e.ReverseCharge ? e.Base * form.GeneralRate / 100m : e.IndirectTax) * e.DeductiblePercent / 100m));

            var result = charged + selfCharged - deductible - credit;
            var estimated = Math.Max(0m, result);

            var lines = new List<TaxFormLine>
            {
                new(null, $"{form.Kind} charged on invoices", charged),
                new(null, "Self-charged on services from abroad (reverse charge)", selfCharged),
                new(null, $"{form.Kind} deductible on expenses", deductible),
                new(null, "Credit carried from earlier quarters", credit),
                new(null, "Result", result)
            };

            decimal? recordedPayment = paid.TryGetValue(quarter, out var amount) ? amount : null;
            forms.Add(new TaxFormPrediction(form.Code, form.Name, Deadline(year, quarter), estimated, recordedPayment, lines));

            credit = Math.Max(0m, -result);
        }

        return forms;
    }
}
