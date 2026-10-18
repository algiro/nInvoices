using nInvoices.Application.Localization;
using nInvoices.Core.TaxPrediction;
using static nInvoices.Countries.Italy.TaxPrediction.ItalianTaxRules;

namespace nInvoices.Countries.Italy.TaxPrediction;

/// <summary>A tax of the year and how it was worked out.</summary>
public sealed record YearAmount(decimal Amount, IReadOnlyList<TaxFormLine> Lines);

/// <summary>The year's income tax and pension contributions of one regime.</summary>
public sealed record IncomeTaxYear(YearAmount Tax, YearAmount Contributions, decimal Revenue);

/// <summary>
/// Regime forfettario: the taxable income is the revenue times the coefficient of the activity (ATECO); the
/// Gestione Separata contributions are due on it, and deducted from it before the 15% (5% for a new activity)
/// imposta sostitutiva. Expenses are not deducted: the coefficient stands for them.
/// </summary>
public static class ForfettarioCalculator
{
    /// <param name="coefficient">Percentage, e.g. 78.</param>
    /// <param name="inpsRate">Percentage, e.g. 26.07.</param>
    public static IncomeTaxYear Calculate(int year, IReadOnlyList<TaxableIncome> income, decimal coefficient, bool startup, decimal inpsRate, IAppText text)
    {
        ArgumentNullException.ThrowIfNull(income);
        ArgumentNullException.ThrowIfNull(text);

        var invoiced = income.Where(i => i.Date.Year == year).ToList();
        var revenue = Round(invoiced.Sum(Revenue));
        var gross = Round(revenue * coefficient / 100m);
        var contributions = Round(gross * inpsRate / 100m);
        var taxable = Math.Max(0m, gross - contributions);
        var rate = startup ? ForfettarioStartupRate : ForfettarioRate;
        var tax = Round(taxable * rate);
        var withheld = Round(invoiced.Sum(i => i.Withheld));

        return new IncomeTaxYear(
            new YearAmount(Math.Max(0m, tax - withheld),
            [
                new("LM22", text.Translate("taxPrediction.italy.lines.revenue"), revenue),
                new(null, text.Translate("taxPrediction.italy.lines.coefficient", ("coefficient", coefficient)), gross),
                new("LM35", text.Translate("taxPrediction.italy.lines.inpsContributions"), contributions),
                new("LM36", text.Translate("taxPrediction.common.taxableIncome"), taxable),
                new("LM39", $"Imposta sostitutiva ({rate * 100m:0}%)", tax),
                new(null, text.Translate("taxPrediction.italy.lines.withheld"), withheld),
                new(null, text.Translate("taxPrediction.common.taxOfTheYear"), Math.Max(0m, tax - withheld))
            ]),
            new YearAmount(contributions,
            [
                new(null, text.Translate("taxPrediction.italy.lines.inpsTaxable"), gross),
                new(null, $"Gestione Separata ({inpsRate}%)", contributions)
            ]),
            revenue);
    }

    /// <summary>What an invoice earns: its base plus the contribution charged to the customer (the rivalsa is income too).</summary>
    internal static decimal Revenue(TaxableIncome invoice) =>
        invoice.Base + invoice.Taxes.Where(t => t.Kind == ItalyComplianceModule.Contribution).Sum(t => t.Amount);
}

/// <summary>
/// Regime ordinario (contabilità semplificata) of a professional: revenue minus expenses is the income; the
/// Gestione Separata contributions are due on it and deducted from it; IRPEF by brackets, minus the
/// self-employment deduction, plus the regional and municipal surcharges; what customers withheld (ritenuta
/// d'acconto) is subtracted.
/// </summary>
public static class OrdinarioCalculator
{
    /// <param name="inpsRate">Percentage, e.g. 26.07.</param>
    /// <param name="surcharges">The regional plus municipal surcharge, percentage.</param>
    public static IncomeTaxYear Calculate(
        int year,
        IReadOnlyList<TaxableIncome> income,
        IReadOnlyList<DeductibleExpense> expenses,
        decimal inpsRate,
        decimal surcharges,
        bool workDeduction,
        IAppText text)
    {
        ArgumentNullException.ThrowIfNull(income);
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(text);

        var invoiced = income.Where(i => i.Date.Year == year).ToList();
        var revenue = Round(invoiced.Sum(ForfettarioCalculator.Revenue));
        var costs = Round(expenses.Where(e => e.Date.Year == year).Sum(e => e.Base * e.DeductiblePercent / 100m));
        var net = revenue - costs;
        var contributions = Round(Math.Max(0m, net) * inpsRate / 100m);
        var taxable = Math.Max(0m, net - contributions);
        var gross = Irpef(taxable, year);
        var deduction = workDeduction ? Math.Min(gross, SelfEmploymentDeduction(taxable)) : 0m;
        var irpef = gross - deduction;
        var surcharge = Round(taxable * surcharges / 100m);
        var withheld = Round(invoiced.Sum(i => i.Withheld));
        var due = irpef + surcharge - withheld;

        return new IncomeTaxYear(
            new YearAmount(Math.Max(0m, due),
            [
                new("RE2", text.Translate("taxPrediction.italy.lines.revenue"), revenue),
                new(null, text.Translate("taxPrediction.italy.lines.deductibleExpenses"), costs),
                new(null, text.Translate("taxPrediction.italy.lines.inpsContributions"), contributions),
                new("RN1", text.Translate("taxPrediction.common.taxableIncome"), taxable),
                new("RN5", text.Translate("taxPrediction.italy.lines.grossIrpef"), gross),
                new("RN7", text.Translate("taxPrediction.italy.lines.workDeduction"), deduction),
                new(null, text.Translate("taxPrediction.italy.lines.surcharges", ("rate", surcharges)), surcharge),
                new("RN33", text.Translate("taxPrediction.italy.lines.withheldRitenute"), withheld),
                new(null, text.Translate(due >= 0 ? "taxPrediction.common.taxOfTheYear" : "taxPrediction.italy.lines.taxCredit"), due)
            ]),
            new YearAmount(contributions,
            [
                new(null, text.Translate("taxPrediction.italy.lines.income"), net),
                new(null, $"Gestione Separata ({inpsRate}%)", contributions)
            ]),
            revenue);
    }
}

/// <summary>
/// The quarterly IVA liquidations: each quarter the IVA charged on its invoices minus the IVA paid on its
/// expenses, plus 1% interest; a credit is carried to the next quarters. The fourth quarter is settled in the
/// annual return. The December installment (acconto IVA) is not estimated.
/// </summary>
public static class IvaLiquidationCalculator
{
    public const string Code = "IVA";

    public static IReadOnlyList<TaxFormPrediction> Calculate(
        int year,
        IReadOnlyList<TaxableIncome> income,
        IReadOnlyList<DeductibleExpense> expenses,
        IReadOnlyDictionary<int, decimal> paid,
        IAppText text)
    {
        ArgumentNullException.ThrowIfNull(income);
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(paid);
        ArgumentNullException.ThrowIfNull(text);

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
                .Where(t => t.Kind is null or ItalyComplianceModule.Iva)
                .Sum(t => t.Amount));
            // A supplier abroad charged no IVA: the user charges it to themselves (integrazione) and deducts it
            var selfCharged = Round(costs.Where(e => e.ReverseCharge).Sum(e => e.Base * IvaGeneralRate / 100m));
            var deductible = Round(costs.Sum(e =>
                (e.ReverseCharge ? e.Base * IvaGeneralRate / 100m : e.IndirectTax) * e.DeductiblePercent / 100m));

            var result = charged + selfCharged - deductible - credit;
            var interest = result > 0 ? Round(result * QuarterlyIvaInterest) : 0m;
            var estimated = Math.Max(0m, result) + interest;

            decimal? recorded = paid.TryGetValue(quarter, out var amount) ? amount : null;
            forms.Add(new TaxFormPrediction(
                Code,
                quarter == 4 ? text.Translate("taxPrediction.italy.iva.lastQuarter") : text.Translate("taxPrediction.italy.iva.quarter", ("quarter", quarter)),
                IvaDeadline(year, quarter),
                estimated,
                recorded,
                [
                    new("VP4", text.Translate("taxPrediction.common.chargedOnInvoices", ("tax", "IVA")), charged),
                    new(null, text.Translate("taxPrediction.common.selfCharged"), selfCharged),
                    new("VP5", text.Translate("taxPrediction.common.deductibleOnExpenses", ("tax", "IVA")), deductible),
                    new("VP8", text.Translate("taxPrediction.common.creditCarried"), credit),
                    new("VP12", text.Translate("taxPrediction.italy.iva.interest"), interest),
                    new("VP14", text.Translate("taxPrediction.common.result"), result + interest)
                ]));

            credit = Math.Max(0m, -result);
        }

        return forms;
    }
}
