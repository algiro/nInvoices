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

    public static readonly IReadOnlyList<ComplianceFieldOption> Natures =
    [
        new("N1", "N1 · Excluded (art. 15)"),
        new("N2.1", "N2.1 · Not subject: arts. 7 to 7-septies (e.g. services to a business abroad)"),
        new("N2.2", "N2.2 · Not subject: other cases (regime forfettario, minimi)"),
        new("N3.1", "N3.1 · Non-taxable: exports"),
        new("N3.2", "N3.2 · Non-taxable: intra-EU supplies"),
        new("N3.3", "N3.3 · Non-taxable: supplies to San Marino"),
        new("N3.4", "N3.4 · Non-taxable: treated as exports"),
        new("N3.5", "N3.5 · Non-taxable: declaration of intent"),
        new("N3.6", "N3.6 · Non-taxable: other"),
        new("N4", "N4 · Exempt"),
        new("N5", "N5 · Margin scheme / IVA not shown"),
        new("N6.1", "N6.1 · Reverse charge: scrap"),
        new("N6.2", "N6.2 · Reverse charge: gold and silver"),
        new("N6.3", "N6.3 · Reverse charge: construction subcontracting"),
        new("N6.4", "N6.4 · Reverse charge: buildings"),
        new("N6.5", "N6.5 · Reverse charge: mobile phones"),
        new("N6.6", "N6.6 · Reverse charge: electronic products"),
        new("N6.7", "N6.7 · Reverse charge: construction services"),
        new("N6.8", "N6.8 · Reverse charge: energy"),
        new("N6.9", "N6.9 · Reverse charge: other cases"),
        new("N7", "N7 · IVA paid in another EU country")
    ];

    private static readonly IReadOnlyList<ComplianceFieldOption> PersonTypes =
    [
        new(Individual, "Individual (libero professionista, ditta individuale)"),
        new(LegalEntity, "Legal entity (company)")
    ];

    private readonly IAppText _text;

    public ItalyComplianceModule(IAppText text)
    {
        _text = text;
    }

    public string CountryCode => CountryCodeValue;

    public string DisplayName => "Italy";

    public IReadOnlySet<ComplianceCapability> Capabilities { get; } = new HashSet<ComplianceCapability>
    {
        ComplianceCapability.IssuerIdentity,
        ComplianceCapability.CustomerFiscalIdentity,
        ComplianceCapability.StructuredEInvoice
    };

    public IReadOnlyList<ComplianceField> Fields { get; } =
    [
        new ComplianceField(
            RegimeKey,
            "Tax regime (regime fiscale)",
            ComplianceFieldType.Choice,
            Required: true,
            Help: "Printed on every FatturaPA. In the regime forfettario no IVA is charged and invoices above €77.47 carry the €2 marca da bollo.",
            Options:
            [
                new ComplianceFieldOption(Ordinario, "RF01 · Ordinario"),
                new ComplianceFieldOption(Minimi, "RF02 · Contribuenti minimi"),
                new ComplianceFieldOption(Forfettario, "RF19 · Forfettario (L. 190/2014)")
            ]),
        new ComplianceField(PersonTypeKey, "Issuer type", ComplianceFieldType.Choice, Required: true,
            Help: "An individual is named by first and last name in the FatturaPA, a company by its legal name.", Options: PersonTypes),
        new ComplianceField(FirstNameKey, "First name (nome)", Help: "Individuals only."),
        new ComplianceField(LastNameKey, "Last name (cognome)", Help: "Individuals only."),
        new ComplianceField(FiscalCodeKey, "Codice fiscale",
            Help: "Required for an individual: the 16-character code. A company's is usually its Partita IVA (the Tax ID above).")
    ];

    public IReadOnlyList<ComplianceField> CustomerFields { get; } =
    [
        new ComplianceField(RecipientCodeKey, "Codice destinatario (SDI)",
            Help: "The 7-character code your customer gave you. Leave it empty for a customer abroad, to send to their PEC, or for a consumer."),
        new ComplianceField(PecKey, "PEC", Help: "The certified email for their e-invoices, if they have no codice destinatario."),
        new ComplianceField(FiscalCodeKey, "Codice fiscale", Help: "For a consumer in Italy without a Partita IVA.")
    ];

    public IReadOnlyList<ComplianceField> TaxFields { get; } =
    [
        new ComplianceField(
            TaxKindKey,
            "What is this tax? (Italy)",
            ComplianceFieldType.Choice,
            Help: "Left empty it is IVA. A pension contribution charged to the customer (rivalsa INPS 4%, cassa professionale) is a contribution: tick \"Part of the taxable amount\" too, so IVA and the ritenuta include it. A ritenuta d'acconto is the tax with a negative rate.",
            Options:
            [
                new ComplianceFieldOption(Iva, "IVA"),
                new ComplianceFieldOption(Contribution, "Pension contribution (rivalsa INPS, cassa)")
            ]),
        new ComplianceField(
            FundTypeKey,
            "Pension fund (tipo cassa)",
            ComplianceFieldType.Choice,
            Help: "For a contribution only. Left empty it is the rivalsa INPS (Gestione Separata).",
            Options:
            [
                new ComplianceFieldOption(InpsFund, "TC22 · INPS (rivalsa, Gestione Separata)"),
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
            "Ritenuta type",
            ComplianceFieldType.Choice,
            Help: "For a withholding only. Left empty it is RT01, the ritenuta d'acconto of an individual.",
            Options:
            [
                new ComplianceFieldOption("RT01", "RT01 · Ritenuta, individuals"),
                new ComplianceFieldOption("RT02", "RT02 · Ritenuta, legal entities"),
                new ComplianceFieldOption("RT03", "RT03 · INPS contribution"),
                new ComplianceFieldOption("RT04", "RT04 · ENASARCO contribution")
            ]),
        new ComplianceField(
            PaymentReasonKey,
            "Reason of the payment (causale)",
            ComplianceFieldType.Choice,
            Help: "For a withholding only, as in the Certificazione Unica. Left empty it is A, professional self-employment.",
            Options:
            [
                new ComplianceFieldOption("A", "A · Professional self-employment"),
                new ComplianceFieldOption("B", "B · Copyright and patents"),
                new ComplianceFieldOption("M", "M · Occasional self-employment"),
                new ComplianceFieldOption("Q", "Q · Single-firm commercial agent"),
                new ComplianceFieldOption("R", "R · Multi-firm commercial agent"),
                new ComplianceFieldOption("ZO", "ZO · Other")
            ]),
        new ComplianceField(
            NatureKey,
            "Why no IVA is charged (natura)",
            ComplianceFieldType.Choice,
            Help: "Required by the FatturaPA for IVA at 0%. Without an IVA tax at all, a forfettario invoice is N2.2.",
            Options: Natures,
            ZeroRateOnly: true)
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

    private static bool HasValue(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex PostalCode();

    [GeneratedRegex(@"^[A-Za-z]{2}$")]
    private static partial Regex Province();

    [GeneratedRegex(@"^[A-Za-z0-9]{7}$")]
    private static partial Regex RecipientCode();
}
