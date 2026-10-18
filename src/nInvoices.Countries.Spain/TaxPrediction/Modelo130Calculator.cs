using nInvoices.Core.TaxPrediction;
using static nInvoices.Countries.Spain.TaxPrediction.SpanishTaxRules;

namespace nInvoices.Countries.Spain.TaxPrediction;

/// <summary>
/// Modelo 130, the quarterly income-tax prepayment of an autónomo in estimación directa. It is
/// cumulative: each quarter takes 20% of the net income from 1 January to the quarter's end and
/// subtracts what was paid in the earlier quarters, so a late expense or a lower estimate corrects itself.
/// </summary>
public static class Modelo130Calculator
{
    public const string Code = "130";

    public const string Name = "Modelo 130 · IRPF prepayment";

    /// <param name="paid">What the user recorded as paid, by quarter. A recorded payment replaces the
    /// estimate of its quarter as the "paid in earlier quarters" of the next ones.</param>
    /// <param name="difficultExpenses">Apply the hard-to-justify expenses deduction.</param>
    /// <param name="deductWithholdings">Subtract the income tax customers withheld (box 06).</param>
    public static IReadOnlyList<TaxFormPrediction> Calculate(
        int year,
        IReadOnlyList<TaxableIncome> income,
        IReadOnlyList<DeductibleExpense> expenses,
        IReadOnlyDictionary<int, decimal> paid,
        bool difficultExpenses,
        bool deductWithholdings)
    {
        ArgumentNullException.ThrowIfNull(income);
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(paid);

        var forms = new List<TaxFormPrediction>();
        var paidBefore = 0m;

        for (var quarter = 1; quarter <= 4; quarter++)
        {
            var end = QuarterEnd(year, quarter);
            var invoiced = income.Where(i => i.Date.Year == year && i.Date <= end).ToList();

            var revenue = Round(invoiced.Sum(i => i.Base));
            var recorded = Round(expenses
                .Where(e => e.Date.Year == year && e.Date <= end)
                .Sum(e => e.Base * e.DeductiblePercent / 100m));
            var beforeDifficult = revenue - recorded;
            var difficult = difficultExpenses && beforeDifficult > 0
                ? Math.Min(Round(beforeDifficult * DifficultExpensesRate(year)), DifficultExpensesCap)
                : 0m;
            var deductible = recorded + difficult;
            var net = revenue - deductible;
            var share = net > 0 ? Round(net * Modelo130Rate) : 0m;
            var withheld = deductWithholdings ? Round(invoiced.Sum(i => i.Withheld)) : 0m;
            var result = share - paidBefore - withheld;
            var estimated = Math.Max(0m, result);

            var lines = new List<TaxFormLine>
            {
                new("01", "Income invoiced since 1 January", revenue),
                new("02", "Deductible expenses since 1 January", deductible),
                new(null, "of which recorded expenses", recorded)
            };
            if (difficultExpenses)
                lines.Add(new(null, "of which hard-to-justify expenses (gastos de difícil justificación)", difficult));
            lines.Add(new("03", "Net income (01 − 02)", net));
            lines.Add(new("04", "20% of the net income", share));
            lines.Add(new("05", "Paid in the earlier quarters", paidBefore));
            lines.Add(new("06", deductWithholdings ? "Withheld by customers since 1 January" : "Withheld by customers (not subtracted: see settings)", withheld));
            lines.Add(new("07", "Result (04 − 05 − 06)", result));

            decimal? recordedPayment = paid.TryGetValue(quarter, out var amount) ? amount : null;
            forms.Add(new TaxFormPrediction(Code, Name, Deadline(year, quarter), estimated, recordedPayment, lines));

            // Box 05 adds up the positive results of the earlier quarters: what was really paid when known
            paidBefore += recordedPayment ?? estimated;
        }

        return forms;
    }
}
