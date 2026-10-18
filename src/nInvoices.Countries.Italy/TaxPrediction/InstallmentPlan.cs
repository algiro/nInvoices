using nInvoices.Application.Localization;
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
        InstallmentPeriods periods,
        IAppText text)
    {
        ArgumentNullException.ThrowIfNull(dueLines);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(paid);
        ArgumentNullException.ThrowIfNull(periods);

        var basis = Math.Max(0m, previousYear ?? due);
        var basisLine = previousYear is null
            ? new TaxFormLine(null, text.Translate("taxPrediction.italy.installments.basisEstimate"), basis)
            : new TaxFormLine(null, text.Translate("taxPrediction.italy.installments.basisLastYear"), basis);

        var (first, second) = isInps
            ? (Round(basis * InpsInstallmentShare), Round(basis * InpsInstallmentShare))
            : TaxInstallments(basis);

        decimal? paidFirst = paid.TryGetValue(periods.First, out var a) ? a : null;
        decimal? paidSecond = paid.TryGetValue(periods.Second, out var b) ? b : null;
        decimal? paidBalance = paid.TryGetValue(periods.Balance, out var c) ? c : null;

        var firstForm = new TaxFormPrediction(code, text.Translate("taxPrediction.italy.installments.first", ("name", name)), JuneDeadline(year), first, paidFirst,
        [
            basisLine,
            new(null, text.Translate(isInps ? "taxPrediction.italy.installments.fortyPercent" : basis <= SingleInstallmentLimit ? "taxPrediction.italy.installments.nothingInJune" : "taxPrediction.italy.installments.fortyPercent"), first)
        ]);

        var secondForm = new TaxFormPrediction(code, text.Translate("taxPrediction.italy.installments.second", ("name", name)), NovemberDeadline(year), second, paidSecond,
        [
            basisLine,
            new(null, text.Translate(isInps ? "taxPrediction.italy.installments.fortyPercent" : basis <= NoInstallmentLimit ? "taxPrediction.italy.installments.nothingUpTo" : basis <= SingleInstallmentLimit ? "taxPrediction.italy.installments.allOfBasis" : "taxPrediction.italy.installments.sixtyPercent"), second)
        ]);

        var onAccount = (paidFirst ?? first) + (paidSecond ?? second);
        var balance = due - onAccount;
        var balanceLines = dueLines.ToList();
        balanceLines.Add(new TaxFormLine(null, text.Translate("taxPrediction.italy.installments.paidOnAccount"), onAccount));
        balanceLines.Add(new TaxFormLine(null, text.Translate(balance >= 0 ? "taxPrediction.italy.installments.balanceToPay" : "taxPrediction.italy.installments.creditNextYear"), balance));

        var balanceForm = new TaxFormPrediction(code, text.Translate("taxPrediction.italy.installments.balance", ("name", name)), JuneDeadline(year + 1), Math.Max(0m, balance), paidBalance, balanceLines);

        return new InstallmentForms(firstForm, secondForm, balanceForm);
    }
}
