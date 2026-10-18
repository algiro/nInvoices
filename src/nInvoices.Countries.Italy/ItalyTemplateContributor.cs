using nInvoices.Application.Compliance;
using nInvoices.Application.Services;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Countries.Italy;

/// <summary>
/// What a template needs to print the Italian parts of an invoice, as <c>compliance.italy</c>: the regime and,
/// for a forfettario, its legal note; the marca da bollo; the pension contribution (rivalsa INPS, cassa); the
/// ritenuta d'acconto and the net to pay. Always present: <c>isApplicable</c> is false, and every part's
/// <c>applies</c> too, for a user without the Italian rules.
/// </summary>
public sealed record ItalyTemplateModel
{
    /// <summary>The note a forfettario invoice carries: why it charges no IVA and suffers no ritenuta.</summary>
    public const string ForfettarioNote =
        "Operazione effettuata ai sensi dell'art. 1, commi da 54 a 89, della Legge n. 190/2014 (regime forfettario): " +
        "operazione senza applicazione dell'IVA e non soggetta a ritenuta d'acconto ai sensi dell'art. 1, comma 67.";

    public bool IsApplicable { get; init; }

    /// <summary>RF01, RF02 or RF19; empty when not applicable.</summary>
    public string Regime { get; init; } = string.Empty;

    public bool IsForfettario { get; init; }

    /// <summary>The forfettario note, or empty.</summary>
    public string LegalNote { get; init; } = string.Empty;

    public ItalyStampDutyModel StampDuty { get; init; } = new();

    public ItalyAmountModel Contribution { get; init; } = new();

    public ItalyAmountModel Withholding { get; init; } = new();

    /// <summary>What the customer pays: the total, the ritenuta already subtracted.</summary>
    public decimal NetToPay { get; init; }
}

public sealed record ItalyStampDutyModel
{
    public const string VirtualNote = "Imposta di bollo da € 2,00 assolta in modo virtuale ai sensi del D.M. 17/06/2014";

    public bool Applies { get; init; }

    public decimal Amount { get; init; }

    public string Note { get; init; } = VirtualNote;
}

/// <summary>A contribution or a ritenuta: all of them together, with the rate of the first.</summary>
public sealed record ItalyAmountModel
{
    public bool Applies { get; init; }

    public string Description { get; init; } = string.Empty;

    public decimal Rate { get; init; }

    public decimal Base { get; init; }

    /// <summary>Always positive, the ritenuta included.</summary>
    public decimal Amount { get; init; }
}

public sealed class ItalyTemplateContributor : IInvoiceTemplateModelContributor
{
    public const string TemplateKey = "italy";

    private readonly IComplianceGate _gate;
    private readonly IRepository<Tax> _taxes;

    public ItalyTemplateContributor(IComplianceGate gate, IRepository<Tax> taxes)
    {
        _gate = gate;
        _taxes = taxes;
    }

    public string Key => TemplateKey;

    public object Empty { get; } = new ItalyTemplateModel();

    public object Sample { get; } = new ItalyTemplateModel
    {
        IsApplicable = true,
        Regime = ItalyComplianceModule.Forfettario,
        IsForfettario = true,
        LegalNote = ItalyTemplateModel.ForfettarioNote,
        StampDuty = new ItalyStampDutyModel { Applies = true, Amount = ItalianInvoiceTaxes.StampDuty },
        Contribution = new ItalyAmountModel { Applies = true, Description = "Rivalsa INPS 4%", Rate = 4m, Base = 1000m, Amount = 40m },
        NetToPay = 1040m
    };

    public async Task<object?> ContributeAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var italy = (await _gate.GetActiveAsync(cancellationToken))
            .FirstOrDefault(a => a.Module.CountryCode == ItalyComplianceModule.CountryCodeValue);
        if (italy is null)
            return null;

        var issuerValues = italy.Settings.ToIssuerProfile().Values;
        var settings = (await _taxes.GetAllAsync(cancellationToken)).Where(t => t.CustomerId == invoice.CustomerId).ToList();
        var taxes = invoice.TaxLines
            .OrderBy(t => t.Order)
            .Select(t => new EInvoiceTax(
                t.IsWithholding ? EInvoiceTaxKind.Withheld : EInvoiceTaxKind.Added,
                t.Description,
                Math.Abs(t.Rate),
                t.BaseAmount.Amount,
                Math.Abs(t.TaxAmount.Amount),
                settings.FirstOrDefault(s => s.TaxId == t.TaxId)?.GetComplianceValues(ItalyComplianceModule.CountryCodeValue)))
            .ToList();

        var italian = ItalianInvoiceTaxes.Of(taxes, invoice.Subtotal.Amount + invoice.TotalExpenses.Amount, issuerValues);
        var regime = issuerValues.GetValueOrDefault(ItalyComplianceModule.RegimeKey) ?? string.Empty;
        var isForfettario = regime == ItalyComplianceModule.Forfettario;

        return new ItalyTemplateModel
        {
            IsApplicable = true,
            Regime = regime,
            IsForfettario = isForfettario,
            LegalNote = isForfettario ? ItalyTemplateModel.ForfettarioNote : string.Empty,
            StampDuty = new ItalyStampDutyModel
            {
                Applies = italian.StampDutyApplies,
                Amount = italian.StampDutyApplies ? ItalianInvoiceTaxes.StampDuty : 0m
            },
            Contribution = Sum(italian.Contributions),
            Withholding = Sum(italian.Withholdings),
            NetToPay = invoice.Total.Amount
        };
    }

    private static ItalyAmountModel Sum(IReadOnlyList<EInvoiceTax> taxes) => taxes.Count == 0
        ? new ItalyAmountModel()
        : new ItalyAmountModel
        {
            Applies = true,
            Description = string.Join(", ", taxes.Select(t => t.Description)),
            Rate = taxes[0].Rate,
            Base = taxes[0].TaxableBase,
            Amount = taxes.Sum(t => t.Amount)
        };
}
