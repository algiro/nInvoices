namespace nInvoices.Core.TaxPrediction;

/// <summary>
/// What a prediction is made from, as plain values in the currency the country taxes in. The caller
/// turns invoices, expenses and payments into these; the module never sees entities.
/// </summary>
/// <param name="Year">The tax year to estimate.</param>
/// <param name="RegimeId">One of the module's <see cref="ITaxPredictionModule.Regimes"/>.</param>
/// <param name="Values">The regime's settings, by field key.</param>
/// <param name="Income">The invoices issued; those outside the year are ignored.</param>
/// <param name="Expenses">The deductible expenses, recurring ones already expanded month by month.</param>
/// <param name="Payments">What the user recorded as actually paid.</param>
public sealed record TaxPredictionInput(
    int Year,
    string RegimeId,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<TaxableIncome> Income,
    IReadOnlyList<DeductibleExpense> Expenses,
    IReadOnlyList<RecordedTaxPayment> Payments);

/// <summary>An issued invoice.</summary>
/// <param name="Date">The issue date: income counts in the period it is invoiced, not paid.</param>
/// <param name="Base">The taxable amount, before indirect taxes and withholdings.</param>
/// <param name="Taxes">Indirect taxes charged on it (IVA, IGIC...); none for a reverse-charge or exempt invoice.</param>
/// <param name="Withheld">Income tax the customer withheld and pays on the user's behalf (positive).</param>
public sealed record TaxableIncome(DateOnly Date, decimal Base, IReadOnlyList<ChargedTax> Taxes, decimal Withheld = 0m);

/// <param name="Kind">Which tax, as the country module names it (e.g. "IVA", "IGIC"); null when the tax doesn't say.</param>
/// <param name="Rate">Percentage, e.g. 7 for 7%.</param>
/// <param name="Amount">The tax charged.</param>
public sealed record ChargedTax(string? Kind, decimal Rate, decimal Amount);

/// <summary>A cost of the business.</summary>
/// <param name="Date">When it was incurred (the supplier invoice date).</param>
/// <param name="Base">The amount without indirect tax.</param>
/// <param name="IndirectTax">Indirect tax paid on it, which can be deducted.</param>
/// <param name="ReverseCharge">
/// A supplier abroad that charged no indirect tax: the user charges it to themselves and deducts it.
/// </param>
/// <param name="DeductiblePercent">How much of it is a business cost, 100 by default.</param>
public sealed record DeductibleExpense(
    DateOnly Date,
    decimal Base,
    decimal IndirectTax = 0m,
    bool ReverseCharge = false,
    decimal DeductiblePercent = 100m);

/// <summary>An amount the user actually paid for one form and period.</summary>
/// <param name="Period">1-based period of the year (the quarter, for a quarterly form).</param>
/// <param name="Form">The form's code, as in <see cref="TaxFormPrediction.Code"/>.</param>
public sealed record RecordedTaxPayment(int Period, string Form, decimal Amount);

/// <param name="Warnings">What the estimate leaves out or could not work out, for the user to see.</param>
public sealed record TaxYearPrediction(int Year, IReadOnlyList<TaxPeriodPrediction> Periods, IReadOnlyList<string> Warnings);

/// <param name="Period">1-based period of the year.</param>
/// <param name="Name">E.g. "Q1".</param>
public sealed record TaxPeriodPrediction(int Period, string Name, DateOnly Start, DateOnly End, IReadOnlyList<TaxFormPrediction> Forms);

/// <summary>One form to file for a period.</summary>
/// <param name="Code">Stable code of the form, e.g. "130".</param>
/// <param name="Deadline">The last day to file and pay it.</param>
/// <param name="Estimated">What is estimated to be paid (never negative).</param>
/// <param name="Paid">What the user recorded as paid, if they did.</param>
/// <param name="Lines">How <paramref name="Estimated"/> was worked out, in the order of the form.</param>
public sealed record TaxFormPrediction(
    string Code,
    string Name,
    DateOnly Deadline,
    decimal Estimated,
    decimal? Paid,
    IReadOnlyList<TaxFormLine> Lines);

/// <param name="Box">The box of the official form, or null for a line that only explains another.</param>
public sealed record TaxFormLine(string? Box, string Label, decimal Amount);
