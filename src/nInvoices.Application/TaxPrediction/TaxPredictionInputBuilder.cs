using nInvoices.Core.Entities;
using nInvoices.Core.TaxPrediction;

namespace nInvoices.Application.TaxPrediction;

/// <summary>
/// Turns the user's records into what a country module estimates from: issued invoices into income,
/// one-off and recurring business expenses into deductible expenses, recorded payments into payments.
/// Pure, so the mapping is tested on plain values. Amounts in another currency than the country's are
/// left out, with a warning.
/// </summary>
public static class TaxPredictionInputBuilder
{
    /// <param name="invoices">The invoices issued in the year, with their tax lines.</param>
    /// <param name="taxes">The user's taxes, to tell which indirect tax each tax line is.</param>
    /// <returns>The input, and what was left out.</returns>
    public static (TaxPredictionInput Input, IReadOnlyList<string> Warnings) Build(
        ITaxPredictionModule module,
        TaxProfile profile,
        TaxYear taxYear,
        IReadOnlyList<Invoice> invoices,
        IReadOnlyList<Tax> taxes,
        IReadOnlyList<BusinessExpense> expenses,
        IReadOnlyList<RecurringExpense> recurring,
        IReadOnlyList<TaxPayment> payments)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(taxes);
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(recurring);
        ArgumentNullException.ThrowIfNull(payments);

        var currency = module.Currency;
        var warnings = new List<string>();

        // A tax line keeps the tax's code, not its id: the customer's tax with that code says what it is
        var taxValues = taxes
            .GroupBy(t => (t.CustomerId, t.TaxId))
            .ToDictionary(g => g.Key, g => g.First().GetComplianceValues(module.CountryCode));

        var counted = invoices.Where(i => i.Total.Currency == currency).ToList();
        if (counted.Count < invoices.Count)
            warnings.Add($"{invoices.Count - counted.Count} invoice(s) in a currency other than {currency} are not counted.");

        var income = counted
            .Select(i => new TaxableIncome(
                i.IssueDate,
                i.Subtotal.Amount + i.TotalExpenses.Amount,
                i.TaxLines
                    .Where(t => !t.IsWithholding && t.TaxAmount.Amount != 0)
                    .Select(t => new ChargedTax(
                        module.IndirectTaxKind(taxValues.GetValueOrDefault((i.CustomerId, t.TaxId)) ?? new Dictionary<string, string>()),
                        t.Rate,
                        t.TaxAmount.Amount))
                    .ToList(),
                i.TaxLines.Where(t => t.IsWithholding).Sum(t => Math.Abs(t.TaxAmount.Amount))))
            .ToList();

        var deductible = expenses
            .Where(e => taxYear.Contains(e.Date) && e.Amount.Currency == currency)
            .Select(e => new DeductibleExpense(e.Date, e.Amount.Amount, e.IndirectTax, e.ReverseCharge, e.DeductiblePercent))
            .Concat(recurring
                .Where(e => e.Amount.Currency == currency)
                .SelectMany(e => e.MonthsBetween(taxYear.Start, taxYear.End).Select(month => new DeductibleExpense(month, e.Amount.Amount))))
            .ToList();

        var otherCurrency = expenses.Count(e => taxYear.Contains(e.Date) && e.Amount.Currency != currency)
            + recurring.Count(e => e.Amount.Currency != currency && e.MonthsBetween(taxYear.Start, taxYear.End).Any());
        if (otherCurrency > 0)
            warnings.Add($"{otherCurrency} expense(s) in a currency other than {currency} are not counted.");

        var recorded = payments
            .Where(p => p.Year == taxYear.Year)
            .Select(p => new RecordedTaxPayment(p.Period, p.Form, p.Amount.Amount))
            .ToList();

        return (new TaxPredictionInput(taxYear.Year, profile.RegimeId, profile.Values, income, deductible, recorded), warnings);
    }
}
