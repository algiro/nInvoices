using nInvoices.Core.Compliance;

namespace nInvoices.Core.TaxPrediction;

/// <summary>
/// The tax rules of one country, used to estimate what a freelancer will pay for each period. Modules
/// are registered in the container and found by <see cref="CountryCode"/>, like the invoicing
/// regimes (<see cref="ICountryComplianceModule"/>), but independent of them: paying taxes in a
/// country doesn't mean issuing its e-invoices. Nothing outside the module knows country rules.
/// </summary>
public interface ITaxPredictionModule
{
    /// <summary>ISO 3166-1 alpha-2 code, upper case.</summary>
    string CountryCode { get; }

    string DisplayName { get; }

    /// <summary>The currency the country taxes in (ISO 4217); amounts in another one are not counted.</summary>
    string Currency { get; }

    /// <summary>The tax regimes the user can choose from, each with its own settings.</summary>
    IReadOnlyList<TaxRegime> Regimes { get; }

    /// <summary>
    /// Which indirect tax (<see cref="ChargedTax.Kind"/>) a tax on an invoice is, from the values the
    /// user gave that tax for this country (<see cref="Entities.Tax.GetComplianceValues"/>); null when
    /// the tax doesn't say, and the module decides from the user's settings.
    /// </summary>
    string? IndirectTaxKind(IReadOnlyDictionary<string, string> taxValues);

    /// <summary>Estimates every period of <see cref="TaxPredictionInput.Year"/>. Pure: no I/O, no clock.</summary>
    /// <exception cref="ArgumentException">The regime is not one of <see cref="Regimes"/>.</exception>
    TaxYearPrediction Predict(TaxPredictionInput input);
}

/// <param name="Id">Stable id the user's choice is stored under.</param>
/// <param name="Name">What the user sees.</param>
/// <param name="Fields">The regime's settings, stored by <see cref="ComplianceField.Key"/>.</param>
/// <param name="Help">Optional explanation shown with the choice.</param>
/// <param name="Defaults">
/// The values a new profile starts with, by field key. A yes/no field is on only when its value is "true"
/// (as everywhere in the settings), so a field meant to start ticked is listed here.
/// </param>
public sealed record TaxRegime(
    string Id,
    string Name,
    IReadOnlyList<ComplianceField> Fields,
    string? Help = null,
    IReadOnlyDictionary<string, string>? Defaults = null);
