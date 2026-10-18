using System.Text.RegularExpressions;
using nInvoices.Application.Localization;
using nInvoices.Core.Compliance;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Countries.Italy;

/// <summary>
/// Italy: the issuer's and customers' fiscal data, the FatturaPA e-invoice (sent to the Sistema di
/// Interscambio by the user or their intermediary), and what an Italian invoice must say: the regime, the
/// marca da bollo, the rivalsa or cassa contribution, the ritenuta d'acconto.
/// </summary>
public sealed partial class ItalyComplianceModule : ICountryComplianceModule
{
    public const string CountryCodeValue = "IT";

    public const string RegimeKey = "regime";
    public const string PersonTypeKey = "personType";
    public const string FirstNameKey = "firstName";
    public const string LastNameKey = "lastName";
    public const string FiscalCodeKey = "fiscalCode";

    /// <summary>Customer: the 7-character SDI code of the channel that receives their e-invoices.</summary>
    public const string RecipientCodeKey = "recipientCode";

    /// <summary>Customer: the certified email (PEC) that receives their e-invoices, when they have no SDI code.</summary>
    public const string PecKey = "pec";

    /// <summary>Tax: which it is, IVA (the default when empty) or a pension contribution (<see cref="Contribution"/>).</summary>
    public const string TaxKindKey = "kind";

    /// <summary>Tax: for a contribution, the fund (TipoCassa, TC22 for the rivalsa INPS).</summary>
    public const string FundTypeKey = "fundType";

    /// <summary>Tax: for a withholding, its type (TipoRitenuta, RT01 by default).</summary>
    public const string WithholdingTypeKey = "withholdingType";

    /// <summary>Tax: for a withholding, the reason of the payment (CausalePagamento, A by default).</summary>
    public const string PaymentReasonKey = "paymentReason";

    /// <summary>Tax: for IVA at 0%, why none is charged (Natura).</summary>
    public const string NatureKey = "nature";

    public const string Ordinario = "RF01";
    public const string Minimi = "RF02";
    public const string Forfettario = "RF19";

    public const string Individual = "individual";
    public const string LegalEntity = "legalEntity";

    public const string Iva = "IVA";
    public const string Contribution = "CASSA";

    /// <summary>The rivalsa INPS of a professional in the Gestione Separata.</summary>
    public const string InpsFund = "TC22";

    /// <summary>A forfettario or minimi invoice charges no IVA: "non soggette, altri casi".</summary>
    public const string NotSubjectOther = "N2.2";

    private readonly IAppText _text;

    public ItalyComplianceModule(IAppText text)
    {
        _text = text;
    }

    public string CountryCode => CountryCodeValue;

    public string DisplayName => _text.Translate("countries.IT");

    public IReadOnlySet<ComplianceCapability> Capabilities { get; } = new HashSet<ComplianceCapability>
    {
        ComplianceCapability.IssuerIdentity,
        ComplianceCapability.CustomerFiscalIdentity,
        ComplianceCapability.StructuredEInvoice
    };

    // Built on each read, in the language of the request
    public IReadOnlyList<ComplianceField> Fields =>
    [
        new ComplianceField(
            RegimeKey,
            T("compliance.italy.fields.regime.label"),
            ComplianceFieldType.Choice,
            Required: true,
            Help: T("compliance.italy.fields.regime.help"),
            Options:
            [
                new ComplianceFieldOption(Ordinario, "RF01 · Ordinario"),
                new ComplianceFieldOption(Minimi, "RF02 · Contribuenti minimi"),
                new ComplianceFieldOption(Forfettario, "RF19 · Forfettario (L. 190/2014)")
            ]),
        new ComplianceField(PersonTypeKey, T("compliance.italy.fields.personType.label"), ComplianceFieldType.Choice, Required: true,
            Help: T("compliance.italy.fields.personType.help"), Options: PersonTypes),
        new ComplianceField(FirstNameKey, T("compliance.italy.fields.firstName.label"), Help: T("compliance.italy.fields.individualsOnly")),
        new ComplianceField(LastNameKey, T("compliance.italy.fields.lastName.label"), Help: T("compliance.italy.fields.individualsOnly")),
        new ComplianceField(FiscalCodeKey, "Codice fiscale", Help: T("compliance.italy.fields.fiscalCode.help"))
    ];

    public IReadOnlyList<ComplianceField> CustomerFields =>
    [
        new ComplianceField(RecipientCodeKey, "Codice destinatario (SDI)", Help: T("compliance.italy.customerFields.recipientCode.help")),
        new ComplianceField(PecKey, "PEC", Help: T("compliance.italy.customerFields.pec.help")),
        new ComplianceField(FiscalCodeKey, "Codice fiscale", Help: T("compliance.italy.customerFields.fiscalCode.help"))
    ];

    public IReadOnlyList<ComplianceField> TaxFields =>
    [
        new ComplianceField(
            TaxKindKey,
            T("compliance.italy.taxFields.kind.label"),
            ComplianceFieldType.Choice,
            Help: T("compliance.italy.taxFields.kind.help"),
            Options:
            [
                new ComplianceFieldOption(Iva, "IVA"),
                new ComplianceFieldOption(Contribution, T("compliance.italy.taxFields.kind.contribution"))
            ]),
        new ComplianceField(
            FundTypeKey,
            T("compliance.italy.taxFields.fundType.label"),
            ComplianceFieldType.Choice,
            Help: T("compliance.italy.taxFields.fundType.help"),
            Options:
            [
                new ComplianceFieldOption(InpsFund, T("compliance.italy.taxFields.fundType.TC22")),
                new ComplianceFieldOption("TC01", "TC01 · Cassa forense"),
                new ComplianceFieldOption("TC02", "TC02 · Dottori commercialisti"),
                new ComplianceFieldOption("TC03", "TC03 · Geometri"),
                new ComplianceFieldOption("TC04", "TC04 · Inarcassa (ingegneri e architetti)"),
                new ComplianceFieldOption("TC06", "TC06 · Ragionieri ed esperti contabili"),
                new ComplianceFieldOption("TC09", "TC09 · ENPAM (medici)"),
                new ComplianceFieldOption("TC17", "TC17 · EPPI (periti industriali)"),
                new ComplianceFieldOption("TC18", "TC18 · EPAP (pluricategoriale)"),
                new ComplianceFieldOption("TC21", "TC21 · ENPAP (psicologi)")
            ]),
        new ComplianceField(
            WithholdingTypeKey,
            T("compliance.italy.taxFields.withholdingType.label"),
            ComplianceFieldType.Choice,
            Help: T("compliance.italy.taxFields.withholdingType.help"),
            Options:
            [
                new ComplianceFieldOption("RT01", T("compliance.italy.taxFields.withholdingType.RT01")),
                new ComplianceFieldOption("RT02", T("compliance.italy.taxFields.withholdingType.RT02")),
                new ComplianceFieldOption("RT03", T("compliance.italy.taxFields.withholdingType.RT03")),
                new ComplianceFieldOption("RT04", T("compliance.italy.taxFields.withholdingType.RT04"))
            ]),
        new ComplianceField(
            PaymentReasonKey,
            T("compliance.italy.taxFields.paymentReason.label"),
            ComplianceFieldType.Choice,
            Help: T("compliance.italy.taxFields.paymentReason.help"),
            Options:
            [
                new ComplianceFieldOption("A", T("compliance.italy.taxFields.paymentReason.A")),
                new ComplianceFieldOption("B", T("compliance.italy.taxFields.paymentReason.B")),
                new ComplianceFieldOption("M", T("compliance.italy.taxFields.paymentReason.M")),
                new ComplianceFieldOption("Q", T("compliance.italy.taxFields.paymentReason.Q")),
                new ComplianceFieldOption("R", T("compliance.italy.taxFields.paymentReason.R")),
                new ComplianceFieldOption("ZO", T("compliance.italy.taxFields.paymentReason.ZO"))
            ]),
        new ComplianceField(
            NatureKey,
            T("compliance.italy.taxFields.nature.label"),
            ComplianceFieldType.Choice,
            Help: T("compliance.italy.taxFields.nature.help"),
            Options: Natures,
            ZeroRateOnly: true)
    ];

    private IReadOnlyList<ComplianceFieldOption> Natures =>
    [
        new ComplianceFieldOption("N1", T("compliance.italy.natures.N1")),
        new ComplianceFieldOption("N2.1", T("compliance.italy.natures.N2_1")),
        new ComplianceFieldOption("N2.2", T("compliance.italy.natures.N2_2")),
        new ComplianceFieldOption("N3.1", T("compliance.italy.natures.N3_1")),
        new ComplianceFieldOption("N3.2", T("compliance.italy.natures.N3_2")),
        new ComplianceFieldOption("N3.3", T("compliance.italy.natures.N3_3")),
        new ComplianceFieldOption("N3.4", T("compliance.italy.natures.N3_4")),
        new ComplianceFieldOption("N3.5", T("compliance.italy.natures.N3_5")),
        new ComplianceFieldOption("N3.6", T("compliance.italy.natures.N3_6")),
        new ComplianceFieldOption("N4", T("compliance.italy.natures.N4")),
        new ComplianceFieldOption("N5", T("compliance.italy.natures.N5")),
        new ComplianceFieldOption("N6.1", T("compliance.italy.natures.N6_1")),
        new ComplianceFieldOption("N6.2", T("compliance.italy.natures.N6_2")),
        new ComplianceFieldOption("N6.3", T("compliance.italy.natures.N6_3")),
        new ComplianceFieldOption("N6.4", T("compliance.italy.natures.N6_4")),
        new ComplianceFieldOption("N6.5", T("compliance.italy.natures.N6_5")),
        new ComplianceFieldOption("N6.6", T("compliance.italy.natures.N6_6")),
        new ComplianceFieldOption("N6.7", T("compliance.italy.natures.N6_7")),
        new ComplianceFieldOption("N6.8", T("compliance.italy.natures.N6_8")),
        new ComplianceFieldOption("N6.9", T("compliance.italy.natures.N6_9")),
        new ComplianceFieldOption("N7", T("compliance.italy.natures.N7"))
    ];

    private IReadOnlyList<ComplianceFieldOption> PersonTypes =>
    [
        new ComplianceFieldOption(Individual, T("compliance.italy.personTypes.individual")),
        new ComplianceFieldOption(LegalEntity, T("compliance.italy.personTypes.legalEntity"))
    ];

    public IReadOnlyList<ComplianceIssue> ValidateIssuer(IssuerProfile issuer)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        var issues = new List<ComplianceIssue>();
        var values = issuer.Values;

        if (string.IsNullOrWhiteSpace(issuer.TaxId))
            issues.Add(new ComplianceIssue("taxId", _text.Translate("compliance.italy.partitaIvaRequired")));
        else if (!ItalianTaxId.IsValidPartitaIva(issuer.TaxId))
            issues.Add(new ComplianceIssue("taxId", _text.Translate("compliance.italy.partitaIvaInvalid")));

        if (issuer.Address is null)
            issues.Add(new ComplianceIssue("address", _text.Translate("compliance.common.addressRequired")));
        else
            issues.AddRange(ValidateItalianAddress(issuer.Address, "address", _text.Translate("compliance.common.subjectAddress")));

        if (values.GetValueOrDefault(RegimeKey) is not (Ordinario or Minimi or Forfettario))
            issues.Add(new ComplianceIssue(RegimeKey, _text.Translate("compliance.italy.chooseRegime")));

        var personType = values.GetValueOrDefault(PersonTypeKey);
        if (personType is not (Individual or LegalEntity))
        {
            issues.Add(new ComplianceIssue(PersonTypeKey, _text.Translate("compliance.common.chooseIssuerType")));
        }
        else if (personType == Individual)
        {
            if (!HasValue(values, FirstNameKey))
                issues.Add(new ComplianceIssue(FirstNameKey, _text.Translate("compliance.common.firstNameRequired")));
            if (!HasValue(values, LastNameKey))
                issues.Add(new ComplianceIssue(LastNameKey, _text.Translate("compliance.italy.lastNameRequired")));
            if (!HasValue(values, FiscalCodeKey))
                issues.Add(new ComplianceIssue(FiscalCodeKey, _text.Translate("compliance.italy.fiscalCodeRequired")));
        }
        else if (string.IsNullOrWhiteSpace(issuer.LegalName))
        {
            issues.Add(new ComplianceIssue("legalName", _text.Translate("compliance.common.legalNameRequired")));
        }

        if (HasValue(values, FiscalCodeKey) && !ItalianTaxId.IsValidCodiceFiscale(values[FiscalCodeKey]))
            issues.Add(new ComplianceIssue(FiscalCodeKey, _text.Translate("compliance.italy.fiscalCodeInvalid")));

        return issues;
    }

    public IReadOnlyList<ComplianceIssue> ValidateCustomer(CustomerProfile customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        var issues = new List<ComplianceIssue>();
        var values = customer.Values;

        if (values.TryGetValue(RecipientCodeKey, out var code) && !RecipientCode().IsMatch(code))
            issues.Add(new ComplianceIssue(RecipientCodeKey, _text.Translate("compliance.italy.recipientCodeInvalid")));
        if (values.TryGetValue(PecKey, out var pec) && !pec.Contains('@', StringComparison.Ordinal))
            issues.Add(new ComplianceIssue(PecKey, _text.Translate("compliance.common.notEmail")));
        if (values.TryGetValue(FiscalCodeKey, out var fiscalCode) && !ItalianTaxId.IsValidCodiceFiscale(fiscalCode))
            issues.Add(new ComplianceIssue(FiscalCodeKey, _text.Translate("compliance.italy.fiscalCodeInvalid")));

        return issues;
    }

    /// <summary>A five-digit CAP and the two-letter province (the "Province / state" of the address).</summary>
    public IEnumerable<ComplianceIssue> ValidateItalianAddress(Address address, string? field, string subject)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!PostalCode().IsMatch(address.ZipCode.Trim()))
            yield return new ComplianceIssue(field, _text.Translate("compliance.italy.postalCode", ("subject", subject)));
        if (!Province().IsMatch(address.State?.Trim() ?? string.Empty))
            yield return new ComplianceIssue(field, _text.Translate("compliance.italy.province", ("subject", subject)));
    }

    /// <summary>Whether the regime charges no IVA (forfettario, minimi).</summary>
    public static bool ChargesNoIva(IReadOnlyDictionary<string, string> issuerValues)
    {
        ArgumentNullException.ThrowIfNull(issuerValues);
        return issuerValues.GetValueOrDefault(RegimeKey) is Forfettario or Minimi;
    }

    /// <summary>Whether a tax is a pension contribution, from its Italian values (<see cref="TaxFields"/>).</summary>
    public static bool IsContribution(IReadOnlyDictionary<string, string>? taxValues) =>
        taxValues?.GetValueOrDefault(TaxKindKey) == Contribution;

    private string T(string key) => _text.Translate(key);

    private static bool HasValue(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex PostalCode();

    [GeneratedRegex(@"^[A-Za-z]{2}$")]
    private static partial Regex Province();

    [GeneratedRegex(@"^[A-Za-z0-9]{7}$")]
    private static partial Regex RecipientCode();
}
