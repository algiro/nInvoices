using System.Text.RegularExpressions;
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

    public string CountryCode => CountryCodeValue;

    public string DisplayName => "Germany";

    public IReadOnlySet<ComplianceCapability> Capabilities { get; } = new HashSet<ComplianceCapability>
    {
        ComplianceCapability.IssuerIdentity,
        ComplianceCapability.CustomerFiscalIdentity,
        ComplianceCapability.StructuredEInvoice
    };

    public IReadOnlyList<ComplianceField> Fields { get; } =
    [
        new ComplianceField(
            SmallBusinessKey,
            "Kleinunternehmer (§ 19 UStG): I charge no VAT",
            ComplianceFieldType.Boolean,
            Help: "Your invoices then carry the § 19 note and no VAT. Only possible below €25,000 of revenue last year and €100,000 this year."),
        new ComplianceField(
            VatIdKey,
            "USt-IdNr",
            Help: "DE and nine digits. Needed for customers in other EU countries; leave it empty if the Tax ID above already is it."),
        new ComplianceField(ContactNameKey, "Contact name", Help: "For XRechnung: who the customer can ask about the invoice."),
        new ComplianceField(ContactPhoneKey, "Contact phone", Help: "For XRechnung."),
        new ComplianceField(ContactEmailKey, "Contact email", Help: "For XRechnung: also the address the invoice comes from."),
        new ComplianceField(IbanKey, "IBAN", Help: "Where the customer pays; XRechnung requires it for a bank transfer."),
        new ComplianceField(BicKey, "BIC", Help: "Optional.")
    ];

    public IReadOnlyList<ComplianceField> CustomerFields { get; } =
    [
        new ComplianceField(
            BuyerReferenceKey,
            "Leitweg-ID / buyer reference",
            Help: "A public administration gives you its Leitweg-ID; a business may give you a reference (a purchase order). Left empty, the invoice number is used."),
        new ComplianceField(BuyerEmailKey, "E-invoice email", Help: "Where their XRechnung goes. Required for XRechnung.")
    ];

    public IReadOnlyList<ComplianceField> TaxFields { get; } =
    [
        new ComplianceField(
            CategoryKey,
            "Why no VAT is charged (Germany)",
            ComplianceFieldType.Choice,
            Help: "Required by XRechnung for VAT at 0%. For a Kleinunternehmer without any VAT tax, the § 19 note is used.",
            Options:
            [
                new ComplianceFieldOption(ReverseCharge, "AE · Reverse charge: the customer owes the VAT (§ 13b UStG, services to an EU business)"),
                new ComplianceFieldOption(IntraCommunity, "K · Intra-community supply of goods"),
                new ComplianceFieldOption(Export, "G · Export outside the EU"),
                new ComplianceFieldOption(NotSubject, "O · Not subject to German VAT (e.g. services to a business outside the EU)"),
                new ComplianceFieldOption(Exempt, "E · Exempt (§ 4 UStG): give the reason below"),
                new ComplianceFieldOption(ZeroRated, "Z · Zero rate (§ 12 (3) UStG, e.g. solar panels)")
            ],
            ZeroRateOnly: true),
        new ComplianceField(
            ExemptionNoteKey,
            "Reason on the invoice",
            Help: "For an exemption, its legal basis (e.g. \"Steuerfrei nach § 4 Nr. 21 UStG\"). The other categories have a default text.",
            ZeroRateOnly: true)
    ];

    public IReadOnlyList<ComplianceIssue> ValidateIssuer(IssuerProfile issuer)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        var issues = new List<ComplianceIssue>();
        var values = issuer.Values;

        if (string.IsNullOrWhiteSpace(issuer.LegalName))
            issues.Add(new ComplianceIssue("legalName", "The legal name is required"));

        if (string.IsNullOrWhiteSpace(issuer.TaxId))
            issues.Add(new ComplianceIssue("taxId", "The Steuernummer or USt-IdNr is required on every invoice (§ 14 UStG)"));
        else if (!GermanTaxId.IsValidVatId(issuer.TaxId) && !GermanTaxId.IsPlausibleTaxNumber(issuer.TaxId))
            issues.Add(new ComplianceIssue("taxId", "This is neither a Steuernummer (10, 11 or 13 digits) nor a valid USt-IdNr"));

        if (HasValue(values, VatIdKey) && !GermanTaxId.IsValidVatId(values[VatIdKey]))
            issues.Add(new ComplianceIssue(VatIdKey, "This is not a valid USt-IdNr (DE and nine digits)"));

        if (issuer.Address is null)
            issues.Add(new ComplianceIssue("address", "The address is required"));
        else
            issues.AddRange(ValidateGermanAddress(issuer.Address, "address", "The address"));

        if (HasValue(values, IbanKey) && !GermanTaxId.IsValidIban(values[IbanKey]))
            issues.Add(new ComplianceIssue(IbanKey, "This is not a valid IBAN"));
        if (HasValue(values, ContactEmailKey) && !values[ContactEmailKey].Contains('@', StringComparison.Ordinal))
            issues.Add(new ComplianceIssue(ContactEmailKey, "This is not an email address"));

        return issues;
    }

    public IReadOnlyList<ComplianceIssue> ValidateCustomer(CustomerProfile customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        var issues = new List<ComplianceIssue>();
        if (customer.Values.TryGetValue(BuyerEmailKey, out var email) && !email.Contains('@', StringComparison.Ordinal))
            issues.Add(new ComplianceIssue(BuyerEmailKey, "This is not an email address"));
        return issues;
    }

    /// <summary>A five-digit Postleitzahl.</summary>
    public static IEnumerable<ComplianceIssue> ValidateGermanAddress(Address address, string? field, string subject)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!PostalCode().IsMatch(address.ZipCode.Trim()))
            yield return new ComplianceIssue(field, $"{subject}: a German Postleitzahl has five digits");
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

    private static bool HasValue(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex PostalCode();
}
