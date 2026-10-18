using System.Text.RegularExpressions;
using nInvoices.Application.Localization;
using nInvoices.Core.Compliance;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Countries.Germany;

/// <summary>
/// Germany: what §14 UStG asks of an invoice (the Steuernummer or USt-IdNr, the Kleinunternehmer note), the
/// customer's data, and the XRechnung e-invoice, which businesses must be able to receive since 2025 and
/// issue from 2027 or 2028.
/// </summary>
public sealed partial class GermanyComplianceModule : ICountryComplianceModule
{
    public const string CountryCodeValue = "DE";

    /// <summary>Issuer: a Kleinunternehmer (§19 UStG) charges no VAT ("true").</summary>
    public const string SmallBusinessKey = "smallBusiness";

    /// <summary>Issuer: the USt-IdNr, when the Tax ID above is the Steuernummer.</summary>
    public const string VatIdKey = "vatId";

    public const string ContactNameKey = "contactName";
    public const string ContactPhoneKey = "contactPhone";
    public const string ContactEmailKey = "contactEmail";
    public const string IbanKey = "iban";
    public const string BicKey = "bic";

    /// <summary>Customer: the Leitweg-ID of a public administration, or the reference the customer gave.</summary>
    public const string BuyerReferenceKey = "buyerReference";

    /// <summary>Customer: the email address their e-invoices go to.</summary>
    public const string BuyerEmailKey = "email";

    /// <summary>Tax: for VAT at 0%, its category (UNCL 5305): why none is charged.</summary>
    public const string CategoryKey = "category";

    /// <summary>Tax: for VAT at 0%, the reason printed on the invoice (an exemption's legal basis).</summary>
    public const string ExemptionNoteKey = "exemptionNote";

    /// <summary>VAT categories (UNCL 5305).</summary>
    public const string Standard = "S";
    public const string ZeroRated = "Z";
    public const string Exempt = "E";
    public const string ReverseCharge = "AE";
    public const string IntraCommunity = "K";
    public const string Export = "G";
    public const string NotSubject = "O";

    public const string SmallBusinessNote = "Gemäß § 19 UStG wird keine Umsatzsteuer berechnet.";
    public const string ReverseChargeNote = "Steuerschuldnerschaft des Leistungsempfängers (Reverse Charge).";

    private readonly IAppText _text;

    public GermanyComplianceModule(IAppText text)
    {
        _text = text;
    }

    public string CountryCode => CountryCodeValue;

    public string DisplayName => _text.Translate("countries.DE");

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
            SmallBusinessKey,
            T("compliance.germany.fields.smallBusiness.label"),
            ComplianceFieldType.Boolean,
            Help: T("compliance.germany.fields.smallBusiness.help")),
        new ComplianceField(
            VatIdKey,
            "USt-IdNr",
            Help: T("compliance.germany.fields.vatId.help")),
        new ComplianceField(ContactNameKey, T("compliance.germany.fields.contactName.label"), Help: T("compliance.germany.fields.contactName.help")),
        new ComplianceField(ContactPhoneKey, T("compliance.germany.fields.contactPhone.label"), Help: T("compliance.germany.fields.contactPhone.help")),
        new ComplianceField(ContactEmailKey, T("compliance.germany.fields.contactEmail.label"), Help: T("compliance.germany.fields.contactEmail.help")),
        new ComplianceField(IbanKey, "IBAN", Help: T("compliance.germany.fields.iban.help")),
        new ComplianceField(BicKey, "BIC", Help: T("compliance.germany.fields.bic.help"))
    ];

    public IReadOnlyList<ComplianceField> CustomerFields =>
    [
        new ComplianceField(
            BuyerReferenceKey,
            T("compliance.germany.customerFields.buyerReference.label"),
            Help: T("compliance.germany.customerFields.buyerReference.help")),
        new ComplianceField(BuyerEmailKey, T("compliance.germany.customerFields.email.label"), Help: T("compliance.germany.customerFields.email.help"))
    ];

    public IReadOnlyList<ComplianceField> TaxFields =>
    [
        new ComplianceField(
            CategoryKey,
            T("compliance.germany.taxFields.category.label"),
            ComplianceFieldType.Choice,
            Help: T("compliance.germany.taxFields.category.help"),
            Options:
            [
                new ComplianceFieldOption(ReverseCharge, T("compliance.germany.taxFields.category.AE")),
                new ComplianceFieldOption(IntraCommunity, T("compliance.germany.taxFields.category.K")),
                new ComplianceFieldOption(Export, T("compliance.germany.taxFields.category.G")),
                new ComplianceFieldOption(NotSubject, T("compliance.germany.taxFields.category.O")),
                new ComplianceFieldOption(Exempt, T("compliance.germany.taxFields.category.E")),
                new ComplianceFieldOption(ZeroRated, T("compliance.germany.taxFields.category.Z"))
            ],
            ZeroRateOnly: true),
        new ComplianceField(
            ExemptionNoteKey,
            T("compliance.germany.taxFields.exemptionNote.label"),
            Help: T("compliance.germany.taxFields.exemptionNote.help"),
            ZeroRateOnly: true)
    ];

    public IReadOnlyList<ComplianceIssue> ValidateIssuer(IssuerProfile issuer)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        var issues = new List<ComplianceIssue>();
        var values = issuer.Values;

        if (string.IsNullOrWhiteSpace(issuer.LegalName))
            issues.Add(new ComplianceIssue("legalName", _text.Translate("compliance.common.legalNameRequired")));

        if (string.IsNullOrWhiteSpace(issuer.TaxId))
            issues.Add(new ComplianceIssue("taxId", _text.Translate("compliance.germany.taxIdRequired")));
        else if (!GermanTaxId.IsValidVatId(issuer.TaxId) && !GermanTaxId.IsPlausibleTaxNumber(issuer.TaxId))
            issues.Add(new ComplianceIssue("taxId", _text.Translate("compliance.germany.taxIdInvalid")));

        if (HasValue(values, VatIdKey) && !GermanTaxId.IsValidVatId(values[VatIdKey]))
            issues.Add(new ComplianceIssue(VatIdKey, _text.Translate("compliance.germany.vatIdInvalid")));

        if (issuer.Address is null)
            issues.Add(new ComplianceIssue("address", _text.Translate("compliance.common.addressRequired")));
        else
            issues.AddRange(ValidateGermanAddress(issuer.Address, "address", _text.Translate("compliance.common.subjectAddress")));

        if (HasValue(values, IbanKey) && !GermanTaxId.IsValidIban(values[IbanKey]))
            issues.Add(new ComplianceIssue(IbanKey, _text.Translate("compliance.germany.ibanInvalid")));
        if (HasValue(values, ContactEmailKey) && !values[ContactEmailKey].Contains('@', StringComparison.Ordinal))
            issues.Add(new ComplianceIssue(ContactEmailKey, _text.Translate("compliance.common.notEmail")));

        return issues;
    }

    public IReadOnlyList<ComplianceIssue> ValidateCustomer(CustomerProfile customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        var issues = new List<ComplianceIssue>();
        if (customer.Values.TryGetValue(BuyerEmailKey, out var email) && !email.Contains('@', StringComparison.Ordinal))
            issues.Add(new ComplianceIssue(BuyerEmailKey, _text.Translate("compliance.common.notEmail")));
        return issues;
    }

    /// <summary>A five-digit Postleitzahl.</summary>
    public IEnumerable<ComplianceIssue> ValidateGermanAddress(Address address, string? field, string subject)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!PostalCode().IsMatch(address.ZipCode.Trim()))
            yield return new ComplianceIssue(field, _text.Translate("compliance.germany.postalCode", ("subject", subject)));
    }

    public static bool IsSmallBusiness(IReadOnlyDictionary<string, string> issuerValues)
    {
        ArgumentNullException.ThrowIfNull(issuerValues);
        return issuerValues.GetValueOrDefault(SmallBusinessKey) == "true";
    }

    /// <summary>The USt-IdNr of the issuer: the field, or the Tax ID when it is one; null if none.</summary>
    public static string? IssuerVatId(string? taxId, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.TryGetValue(VatIdKey, out var vatId) && GermanTaxId.IsValidVatId(vatId))
            return GermanTaxId.Normalize(vatId);
        return GermanTaxId.IsValidVatId(taxId) ? GermanTaxId.Normalize(taxId) : null;
    }

    /// <summary>The Steuernummer of the issuer: the Tax ID unless it is the USt-IdNr; null if none.</summary>
    public static string? IssuerTaxNumber(string? taxId) =>
        !string.IsNullOrWhiteSpace(taxId) && !GermanTaxId.IsValidVatId(taxId) ? taxId.Trim() : null;

    private string T(string key) => _text.Translate(key);

    private static bool HasValue(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex PostalCode();
}
