using nInvoices.Core.TaxPrediction;
using static nInvoices.Countries.Italy.TaxPrediction.ItalianTaxRules;

namespace nInvoices.Countries.Italy.TaxPrediction;

/// <summary>The three payments of a tax of the year: two installments (acconti) during it, the balance (saldo) after.</summary>
public sealed record InstallmentForms(TaxFormPrediction First, TaxFormPrediction Second, TaxFormPrediction Balance);

/// <summary>Which period of the year each payment is recorded under (<see cref="RecordedTaxPayment.Period"/>).</summary>
public sealed record InstallmentPeriods(int First, int Second, int Balance);

/// <summary>
/// The acconti and the saldo of a yearly tax. The installments are worked out on last year's amount when the user
/// gave it (metodo storico, the legal default) or else on this year's estimate (metodo previsionale); the balance
/// is the year's amount minus the installments, as paid when recorded.
/// </summary>
public static class InstallmentPlan
{
    /// <param name="due">The year's amount, after what customers withheld.</param>
    /// <param name="dueLines">How <paramref name="due"/> was worked out, shown on the balance.</param>
    /// <param name="previousYear">Last year's amount, for the metodo storico; null for the previsionale.</param>
    /// <param name="isInps">INPS installments are 40% and 40%; the income tax's 40% and 60%, with the thresholds.</param>
    public static InstallmentForms Calculate(
        string code,
        string name,
        int year,
        decimal due,
        IReadOnlyList<TaxFormLine> dueLines,
        decimal? previousYear,
        bool isInps,
        IReadOnlyDictionary<int, decimal> paid,
        InstallmentPeriods periods)
    {
        ArgumentNullException.ThrowIfNull(dueLines);
        ArgumentNullException.ThrowIfNull(paid);
        ArgumentNullException.ThrowIfNull(periods);

        var basis = Math.Max(0m, previousYear ?? due);
        var basisLine = previousYear is null
            ? new TaxFormLine(null, "Basis: this year's estimate (metodo previsionale)", basis)
            : new TaxFormLine(null, "Basis: last year's amount (metodo storico)", basis);

        var (first, second) = isInps
            ? (Round(basis * InpsInstallmentShare), Round(basis * InpsInstallmentShare))
            : TaxInstallments(basis);

        decimal? paidFirst = paid.TryGetValue(periods.First, out var a) ? a : null;
        decimal? paidSecond = paid.TryGetValue(periods.Second, out var b) ? b : null;
        decimal? paidBalance = paid.TryGetValue(periods.Balance, out var c) ? c : null;

        var firstForm = new TaxFormPrediction(code, $"{name} · 1st installment", JuneDeadline(year), first, paidFirst,
        [
            basisLine,
            new(null, isInps ? "40% of the basis" : basis <= SingleInstallmentLimit ? "Nothing in June below €257.52" : "40% of the basis", first)
        ]);

        var secondForm = new TaxFormPrediction(code, $"{name} · 2nd installment", NovemberDeadline(year), second, paidSecond,
        [
            basisLine,
            new(null, isInps ? "40% of the basis" : basis <= NoInstallmentLimit ? "Nothing up to €51.65" : basis <= SingleInstallmentLimit ? "All of the basis" : "60% of the basis", second)
        ]);

        var onAccount = (paidFirst ?? first) + (paidSecond ?? second);
        var balance = due - onAccount;
        var balanceLines = dueLines.ToList();
        balanceLines.Add(new TaxFormLine(null, "Paid on account (installments)", onAccount));
        balanceLines.Add(new TaxFormLine(null, balance >= 0 ? "Balance to pay" : "Credit for next year", balance));

        var balanceForm = new TaxFormPrediction(code, $"{name} · balance", JuneDeadline(year + 1), Math.Max(0m, balance), paidBalance, balanceLines);

        return new InstallmentForms(firstForm, secondForm, balanceForm);
    }
}
