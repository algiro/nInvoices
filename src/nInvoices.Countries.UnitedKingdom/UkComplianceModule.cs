using nInvoices.Application.Localization;
using nInvoices.Core.Compliance;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Countries.UnitedKingdom;

/// <summary>
/// The United Kingdom: what a VAT invoice must show (the VAT number, the VAT of each rate, why a 0% line charges
/// none) for a VAT-registered business, and the trading details of one that isn't. No e-invoice is mandatory.
/// </summary>
public sealed class UkComplianceModule : ICountryComplianceModule
{
    public const string CountryCodeValue = "GB";

    /// <summary>Issuer: registered for VAT ("true"): the Tax ID is then the VAT number.</summary>
    public const string VatRegisteredKey = "vatRegistered";

    /// <summary>Issuer: the name a sole trader trades under, if not their own.</summary>
    public const string TradingNameKey = "tradingName";

    /// <summary>Tax: for VAT at 0%, why none is charged.</summary>
    public const string TreatmentKey = "treatment";

    public const string ZeroRated = "zero";
    public const string Exempt = "exempt";
    public const string OutsideScope = "outside";
    public const string ReverseCharge = "reverseCharge";

    /// <summary>What the invoice says for each treatment: legal wording, in English as the invoice is.</summary>
    public static IReadOnlyDictionary<string, string> TreatmentNotes { get; } = new Dictionary<string, string>
    {
        [ZeroRated] = "Zero-rated supply.",
        [Exempt] = "Exempt from VAT.",
        [OutsideScope] = "Outside the scope of UK VAT.",
        [ReverseCharge] = "Reverse charge: customer to pay the VAT to HMRC."
    };

    private readonly IAppText _text;

    public UkComplianceModule(IAppText text)
    {
        _text = text;
    }

    public string CountryCode => CountryCodeValue;

    public string DisplayName => _text.Translate("countries.GB");

    public IReadOnlySet<ComplianceCapability> Capabilities { get; } = new HashSet<ComplianceCapability>
    {
        ComplianceCapability.IssuerIdentity
    };

    // Built on each read, in the language of the request
    public IReadOnlyList<ComplianceField> Fields =>
    [
        new ComplianceField(VatRegisteredKey, T("compliance.uk.fields.vatRegistered.label"), ComplianceFieldType.Boolean,
            Help: T("compliance.uk.fields.vatRegistered.help")),
        new ComplianceField(TradingNameKey, T("compliance.uk.fields.tradingName.label"), Help: T("compliance.uk.fields.tradingName.help"))
    ];

    public IReadOnlyList<ComplianceField> CustomerFields => [];

    public IReadOnlyList<ComplianceField> TaxFields =>
    [
        new ComplianceField(
            TreatmentKey,
            T("compliance.uk.taxFields.treatment.label"),
            ComplianceFieldType.Choice,
            Help: T("compliance.uk.taxFields.treatment.help"),
            Options:
            [
                new ComplianceFieldOption(ZeroRated, T("compliance.uk.taxFields.treatment.zero")),
                new ComplianceFieldOption(Exempt, T("compliance.uk.taxFields.treatment.exempt")),
                new ComplianceFieldOption(OutsideScope, T("compliance.uk.taxFields.treatment.outside")),
                new ComplianceFieldOption(ReverseCharge, T("compliance.uk.taxFields.treatment.reverseCharge"))
            ],
            ZeroRateOnly: true)
    ];

    public IReadOnlyList<ComplianceIssue> ValidateIssuer(IssuerProfile issuer)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        var issues = new List<ComplianceIssue>();

        if (string.IsNullOrWhiteSpace(issuer.LegalName))
            issues.Add(new ComplianceIssue("legalName", _text.Translate("compliance.common.legalNameRequired")));

        var registered = IsVatRegistered(issuer.Values);
        if (string.IsNullOrWhiteSpace(issuer.TaxId))
        {
            if (registered)
                issues.Add(new ComplianceIssue("taxId", _text.Translate("compliance.uk.vatNumberRequired")));
        }
        else if (registered && !UkTaxId.IsValidVatNumber(issuer.TaxId))
        {
            issues.Add(new ComplianceIssue("taxId", _text.Translate("compliance.uk.vatNumberInvalid")));
        }
        else if (!registered && !UkTaxId.IsValidVatNumber(issuer.TaxId) && !UkTaxId.IsPlausibleUtr(issuer.TaxId))
        {
            issues.Add(new ComplianceIssue("taxId", _text.Translate("compliance.uk.taxIdInvalid")));
        }

        if (issuer.Address is null)
            issues.Add(new ComplianceIssue("address", _text.Translate("compliance.common.addressRequired")));
        else
            issues.AddRange(ValidateUkAddress(issuer.Address, "address", _text.Translate("compliance.common.subjectAddress")));

        return issues;
    }

    public IReadOnlyList<ComplianceIssue> ValidateCustomer(CustomerProfile customer) => [];

    /// <summary>A valid postcode and a town.</summary>
    public IEnumerable<ComplianceIssue> ValidateUkAddress(Address address, string? field, string subject)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!UkTaxId.IsValidPostcode(address.ZipCode))
            yield return new ComplianceIssue(field, _text.Translate("compliance.uk.postcodeInvalid", ("subject", subject)));
        if (string.IsNullOrWhiteSpace(address.City))
            yield return new ComplianceIssue(field, _text.Translate("compliance.uk.townRequired", ("subject", subject)));
    }

    public static bool IsVatRegistered(IReadOnlyDictionary<string, string> issuerValues)
    {
        ArgumentNullException.ThrowIfNull(issuerValues);
        return issuerValues.GetValueOrDefault(VatRegisteredKey) == "true";
    }

    private string T(string key) => _text.Translate(key);
}
