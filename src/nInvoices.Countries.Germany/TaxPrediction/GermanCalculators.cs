using nInvoices.Application.Localization;
using nInvoices.Core.TaxPrediction;
using static nInvoices.Countries.Germany.TaxPrediction.GermanTaxRules;

namespace nInvoices.Countries.Germany.TaxPrediction;

/// <summary>A tax of the year and how it was worked out.</summary>
public sealed record GermanYearAmount(decimal Amount, IReadOnlyList<TaxFormLine> Lines);

/// <summary>The income taxes of the year, the Gewerbesteuer (zero for a Freiberufler), revenue and profit.</summary>
public sealed record GermanIncomeYear(GermanYearAmount IncomeTax, GermanYearAmount TradeTax, decimal Revenue, decimal Profit);

/// <summary>
/// The Einkommensteuer of a self-employed person (Einnahmenüberschussrechnung, single assessment): the profit
/// less the deductible insurance and the Sonderausgaben-Pauschbetrag, taxed by § 32a EStG; for a trade, the
/// Gewerbesteuer, part of which is credited against it (§ 35 EStG); then the Soli and the Kirchensteuer.
/// </summary>
public static class GermanIncomeTaxCalculator
{
    /// <param name="hebesatz">The municipality's Hebesatz as a percentage (e.g. 400); null for a Freiberufler (no Gewerbesteuer).</param>
    /// <param name="insurance">Deductible health, care and pension contributions of the year (Vorsorgeaufwendungen).</param>
    /// <param name="churchRate">Kirchensteuer, a percentage of the Einkommensteuer (8 or 9), or 0.</param>
    public static GermanIncomeYear Calculate(
        int year,
        IReadOnlyList<TaxableIncome> income,
        IReadOnlyList<DeductibleExpense> expenses,
        decimal? hebesatz,
        decimal insurance,
        decimal churchRate,
        IAppText text)
    {
        ArgumentNullException.ThrowIfNull(income);
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(text);

        var revenue = Round(income.Where(i => i.Date.Year == year).Sum(i => i.Base));
        var costs = Round(expenses.Where(e => e.Date.Year == year).Sum(e => e.Base * e.DeductiblePercent / 100m));
        var profit = revenue - costs;

        // Gewerbesteuer, for a trade
        var tradeLines = new List<TaxFormLine>();
        decimal tradeTax = 0m, baseAmount = 0m;
        if (hebesatz is { } rate)
        {
            baseAmount = TradeTaxBase(profit);
            tradeTax = Round(baseAmount * rate / 100m);
            tradeLines.Add(new(null, text.Translate("taxPrediction.germany.lines.tradeProfit"), Math.Floor(Math.Max(0m, profit) / 100m) * 100m));
            tradeLines.Add(new(null, text.Translate("taxPrediction.germany.lines.tradeAllowance"), Math.Min(TradeTaxAllowance, Math.Max(0m, profit))));
            tradeLines.Add(new(null, "Steuermessbetrag (3.5%)", baseAmount));
            tradeLines.Add(new(null, text.Translate("taxPrediction.germany.lines.tradeTax", ("rate", rate)), tradeTax));
        }

        var taxable = Math.Max(0m, profit - insurance - SpecialExpensesAllowance);
        var incomeTax = IncomeTax(taxable, year);
        var credit = Math.Min(Math.Min(Round(baseAmount * TradeTaxCreditFactor), tradeTax), incomeTax);
        var assessed = incomeTax - credit;
        var solidarity = SolidaritySurcharge(assessed, year);
        // The Kirchensteuer is on the tax before the Gewerbesteuer credit (§ 51a (2) EStG)
        var church = Round(incomeTax * churchRate / 100m);
        var total = assessed + solidarity + church;

        var lines = new List<TaxFormLine>
        {
            new(null, text.Translate("taxPrediction.germany.lines.revenue"), revenue),
            new(null, text.Translate("taxPrediction.germany.lines.businessExpenses"), costs),
            new(null, text.Translate("taxPrediction.germany.lines.profit"), profit),
            new(null, text.Translate("taxPrediction.germany.lines.insurance"), insurance),
            new(null, "− Sonderausgaben-Pauschbetrag", SpecialExpensesAllowance),
            new(null, text.Translate("taxPrediction.germany.lines.taxable"), taxable),
            new(null, "Einkommensteuer (Grundtarif)", incomeTax)
        };
        if (hebesatz is not null)
            lines.Add(new(null, text.Translate("taxPrediction.germany.lines.tradeTaxCredit"), credit));
        lines.Add(new(null, "Solidaritätszuschlag", solidarity));
        lines.Add(new(null, $"Kirchensteuer ({churchRate}%)", church));
        lines.Add(new(null, text.Translate("taxPrediction.common.taxOfTheYear"), total));

        return new GermanIncomeYear(new GermanYearAmount(total, lines), new GermanYearAmount(tradeTax, tradeLines), revenue, profit);
    }
}

/// <summary>
/// The Umsatzsteuer-Voranmeldung of each month or quarter: the VAT charged on its invoices minus the input
/// VAT of its expenses. A surplus of input VAT is refunded, not carried on.
/// </summary>
public static class VatReturnCalculator
{
    public const string Code = "USt";

    /// <param name="monthly">Monthly returns; quarterly otherwise.</param>
    /// <param name="extension">Dauerfristverlängerung: one month more for each return.</param>
    /// <param name="paid">What the user recorded as paid, by period (the month, or the quarter).</param>
    public static IReadOnlyList<(int Period, string Name, DateOnly Start, DateOnly End, TaxFormPrediction Form)> Calculate(
        int year,
        IReadOnlyList<TaxableIncome> income,
        IReadOnlyList<DeductibleExpense> expenses,
        bool monthly,
        bool extension,
        IReadOnlyDictionary<int, decimal> paid,
        IAppText text)
    {
        ArgumentNullException.ThrowIfNull(income);
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(paid);
        ArgumentNullException.ThrowIfNull(text);

        var count = monthly ? 12 : 4;
        var months = monthly ? 1 : 3;
        var result = new List<(int, string, DateOnly, DateOnly, TaxFormPrediction)>();

        for (var period = 1; period <= count; period++)
        {
            var start = new DateOnly(year, (period - 1) * months + 1, 1);
            var end = start.AddMonths(months).AddDays(-1);
            var name = monthly ? Month(start.Month, text) : text.Translate("taxPrediction.common.quarter", ("quarter", period));
            var costs = expenses.Where(e => e.Date >= start && e.Date <= end).ToList();

            var charged = Round(income
                .Where(i => i.Date >= start && i.Date <= end)
                .SelectMany(i => i.Taxes)
                .Where(t => t.Kind is null or GermanyTaxPredictionModule.VatKind)
                .Sum(t => t.Amount));
            // A supplier abroad charged no VAT: the user owes it (§ 13b UStG) and deducts it as input VAT
            var reverseBase = Round(costs.Where(e => e.ReverseCharge).Sum(e => e.Base));
            var reverseTax = Round(reverseBase * VatGeneralRate / 100m);
            var inputVat = Round(costs.Where(e => !e.ReverseCharge).Sum(e => e.IndirectTax * e.DeductiblePercent / 100m));
            var reverseInput = Round(costs.Where(e => e.ReverseCharge).Sum(e => e.Base * VatGeneralRate / 100m * e.DeductiblePercent / 100m));
            var balance = charged + reverseTax - inputVat - reverseInput;

            decimal? recorded = paid.TryGetValue(period, out var amount) ? amount : null;
            result.Add((period, name, start, end, new TaxFormPrediction(
                Code,
                $"USt-Voranmeldung · {name}",
                VatDeadline(end, extension),
                Math.Max(0m, balance),
                recorded,
                [
                    new(null, text.Translate("taxPrediction.germany.vat.charged"), charged),
                    new("46", text.Translate("taxPrediction.germany.vat.reverseBase"), reverseBase),
                    new("47", text.Translate("taxPrediction.germany.vat.reverseTax"), reverseTax),
                    new("66", text.Translate("taxPrediction.germany.vat.inputVat"), inputVat),
                    new("67", text.Translate("taxPrediction.germany.vat.reverseInput"), reverseInput),
                    new("83", text.Translate(balance >= 0 ? "taxPrediction.germany.vat.toPay" : "taxPrediction.germany.refund"), balance)
                ])));
        }

        return result;
    }

    private static string Month(int month, IAppText text) => month switch
    {
        1 => text.Translate("taxPrediction.common.months.january"),
        2 => text.Translate("taxPrediction.common.months.february"),
        3 => text.Translate("taxPrediction.common.months.march"),
        4 => text.Translate("taxPrediction.common.months.april"),
        5 => text.Translate("taxPrediction.common.months.may"),
        6 => text.Translate("taxPrediction.common.months.june"),
        7 => text.Translate("taxPrediction.common.months.july"),
        8 => text.Translate("taxPrediction.common.months.august"),
        9 => text.Translate("taxPrediction.common.months.september"),
        10 => text.Translate("taxPrediction.common.months.october"),
        11 => text.Translate("taxPrediction.common.months.november"),
        _ => text.Translate("taxPrediction.common.months.december")
    };
}
