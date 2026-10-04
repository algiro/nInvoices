using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using nInvoices.Core.Compliance;
using nInvoices.Core.Configuration;

namespace nInvoices.Application.Compliance.Spain;

/// <summary>
/// Spain: Facturae and FACe (invoices to public administrations), Verifactu and, later, B2B
/// e-invoicing. Each phase adds its capability and fields here.
/// </summary>
public sealed partial class SpainComplianceModule : ICountryComplianceModule
{
    public const string CountryCodeValue = "ES";

    public const string PersonTypeKey = "personType";
    public const string FirstNameKey = "firstName";
    public const string FirstSurnameKey = "firstSurname";
    public const string SecondSurnameKey = "secondSurname";

    public const string IsPublicAdministrationKey = "isPublicAdministration";
    public const string AccountingOfficeKey = "dir3AccountingOffice";
    public const string ManagingBodyKey = "dir3ManagingBody";
    public const string ProcessingUnitKey = "dir3ProcessingUnit";

    /// <summary>Issuer setting: issue Verifactu records (hash-chained, sent to AEAT) for every invoice.</summary>
    public const string VerifactuKey = "verifactu";

    /// <summary>Issuer setting: the email FACe notifies about the invoices sent through it.</summary>
    public const string FaceEmailKey = "faceEmail";

    /// <summary>Issuer setting: the signing certificate is a company seal certificate (certificado de sello).</summary>
    public const string SealCertificateKey = "sealCertificate";

    /// <summary>Tax setting: how a 0% VAT is treated (S1, S2, N1, N2, E1..E8).</summary>
    public const string OperationKey = "operation";

    /// <summary>Tax setting: which indirect tax it is, IVA (the default when empty) or IGIC (Canary Islands).</summary>
    public const string TaxTypeKey = "taxType";

    public const string Iva = "IVA";
    public const string Igic = "IGIC";

    public const string Individual = "individual";
    public const string LegalEntity = "legalEntity";

    private static readonly IReadOnlyList<ComplianceFieldOption> PersonTypes =
    [
        new ComplianceFieldOption(Individual, "Individual (autónomo)"),
        new ComplianceFieldOption(LegalEntity, "Legal entity (company)")
    ];

    private readonly VerifactuOptions _verifactu;

    public SpainComplianceModule(IOptions<VerifactuOptions>? verifactu = null)
    {
        _verifactu = verifactu?.Value ?? new VerifactuOptions();
    }

    public string CountryCode => CountryCodeValue;

    public string DisplayName => "Spain";

    public IReadOnlySet<ComplianceCapability> Capabilities { get; } = new HashSet<ComplianceCapability>
    {
        ComplianceCapability.IssuerIdentity,
        ComplianceCapability.CustomerFiscalIdentity,
        ComplianceCapability.StructuredEInvoice,
        ComplianceCapability.ElectronicSignature,
        ComplianceCapability.TamperEvidentRecords,
        ComplianceCapability.VerificationMark
    };

    public IReadOnlyList<ComplianceField> Fields { get; } =
    [
        new ComplianceField(
            PersonTypeKey,
            "Issuer type",
            ComplianceFieldType.Choice,
            Required: true,
            Help: "Facturae distinguishes a natural person (autónomo) from a legal entity.",
            Options: PersonTypes),
        new ComplianceField(FirstNameKey, "First name", Help: "Individuals only: the legal name is split into first name and surnames."),
        new ComplianceField(FirstSurnameKey, "First surname", Help: "Individuals only."),
        new ComplianceField(SecondSurnameKey, "Second surname", Help: "Individuals only, if any."),
        new ComplianceField(
            VerifactuKey,
            "Verifactu",
            ComplianceFieldType.Boolean,
            Help: "Every invoice you issue is recorded in a tamper-evident chain and reported to the Tax Agency (AEAT), and carries a QR code. Required for autónomos from 1 July 2027."),
        new ComplianceField(
            FaceEmailKey,
            "Email for FACe notifications",
            Help: "FACe tells you here when an invoice you send to a public administration is registered, rejected or paid. Needed to send invoices to FACe."),
        new ComplianceField(
            SealCertificateKey,
            "My certificate is a company seal (certificado de sello)",
            ComplianceFieldType.Boolean,
            Help: "For Verifactu: tick it only if the certificate you upload is an electronic seal of a company, not a personal certificate.")
    ];

    public IReadOnlyList<ComplianceField> TaxFields { get; } =
    [
        new ComplianceField(
            TaxTypeKey,
            "Which tax is this?",
            ComplianceFieldType.Choice,
            Help: "Left empty it is IVA. Choose IGIC (Impuesto General Indirecto Canario) if you invoice from the Canary Islands. A withholding such as IRPF is not affected: it is the tax with a negative rate.",
            Options:
            [
                new ComplianceFieldOption(Iva, "IVA (VAT)"),
                new ComplianceFieldOption(Igic, "IGIC (Canary Islands)")
            ]),
        new ComplianceField(
            OperationKey,
            "Verifactu treatment of a 0% tax",
            ComplianceFieldType.Choice,
            Help: "Only used when the rate is 0%: why no tax is charged. A tax above 0% is always taxed. The article of each exemption depends on the tax (E7 and E8 exist for IGIC only).",
            Options:
            [
                new ComplianceFieldOption("S1", "Taxed at 0% (a zero rate, e.g. IGIC tipo cero)"),
                new ComplianceFieldOption("S2", "Taxable, reverse charge (inversión del sujeto pasivo)"),
                new ComplianceFieldOption("N2", "Not subject: place-of-supply rules (e.g. services to a business in another country)"),
                new ComplianceFieldOption("N1", "Not subject: article 7, 14 or other (IGIC: article 9)"),
                new ComplianceFieldOption("E1", "Exempt: E1 (IVA article 20)"),
                new ComplianceFieldOption("E2", "Exempt: E2 (IVA article 21, exports; IGIC article 11)"),
                new ComplianceFieldOption("E3", "Exempt: E3 (IVA article 22; IGIC article 12)"),
                new ComplianceFieldOption("E4", "Exempt: E4 (IVA articles 23 and 24; IGIC article 13)"),
                new ComplianceFieldOption("E5", "Exempt: E5 (IVA article 25, intra-community supplies)"),
                new ComplianceFieldOption("E6", "Exempt: E6 (other reasons)"),
                new ComplianceFieldOption("E7", "Exempt: E7 (IGIC only: article 90 of the Canary Islands consolidated text)"),
                new ComplianceFieldOption("E8", "Exempt: E8 (IGIC only: Ley 20/1991)")
            ])
    ];

    public IReadOnlyList<ComplianceField> CustomerFields { get; } =
    [
        new ComplianceField(
            IsPublicAdministrationKey,
            "Public administration",
            ComplianceFieldType.Boolean,
            Help: "Invoices to public bodies go through FACe and need the three DIR3 codes below."),
        new ComplianceField(AccountingOfficeKey, "Accounting office (oficina contable)", Help: "DIR3 code. Public administrations only."),
        new ComplianceField(ManagingBodyKey, "Managing body (órgano gestor)", Help: "DIR3 code. Public administrations only."),
        new ComplianceField(ProcessingUnitKey, "Processing unit (unidad tramitadora)", Help: "DIR3 code. Public administrations only."),
        new ComplianceField(
            PersonTypeKey,
            "Customer type",
            ComplianceFieldType.Choice,
            Help: "Left empty it is worked out from the tax id.",
            Options: PersonTypes),
        new ComplianceField(FirstSurnameKey, "First surname", Help: "Individuals only; the customer name is then the first name."),
        new ComplianceField(SecondSurnameKey, "Second surname", Help: "Individuals only, if any.")
    ];

    public IReadOnlyList<ComplianceIssue> ValidateIssuer(IssuerProfile issuer)
    {
        var issues = new List<ComplianceIssue>();

        if (string.IsNullOrWhiteSpace(issuer.LegalName))
            issues.Add(new ComplianceIssue("legalName", "The legal name is required"));

        if (string.IsNullOrWhiteSpace(issuer.TaxId))
            issues.Add(new ComplianceIssue("taxId", "The NIF is required"));
        else if (!SpanishTaxId.IsValid(issuer.TaxId))
            issues.Add(new ComplianceIssue("taxId", "This is not a valid Spanish NIF, NIE or CIF"));

        if (issuer.Address is null)
            issues.Add(new ComplianceIssue("address", "The address is required"));
        else
            issues.AddRange(ValidateSpanishAddress(issuer.Address, "address", "The address"));

        if (!issuer.Values.TryGetValue(PersonTypeKey, out var personType) || personType is not (Individual or LegalEntity))
        {
            issues.Add(new ComplianceIssue(PersonTypeKey, "Choose whether the issuer is an individual or a legal entity"));
        }
        else if (personType == Individual)
        {
            if (!HasValue(issuer.Values, FirstNameKey))
                issues.Add(new ComplianceIssue(FirstNameKey, "An individual needs a first name"));
            if (!HasValue(issuer.Values, FirstSurnameKey))
                issues.Add(new ComplianceIssue(FirstSurnameKey, "An individual needs a first surname"));
        }

        if (issuer.Values.GetValueOrDefault(VerifactuKey) == "true")
        {
            var missing = _verifactu.Missing();
            if (missing.Count > 0)
                issues.Add(new ComplianceIssue(VerifactuKey,
                    $"Verifactu is not set up on this server: {string.Join(", ", missing)} missing under {VerifactuOptions.SectionName}"));
            if (!string.IsNullOrWhiteSpace(issuer.TaxId) && SpanishTaxId.Normalize(issuer.TaxId).Length != 9)
                issues.Add(new ComplianceIssue("taxId", "Verifactu needs a nine-character NIF"));
        }

        return issues;
    }

    public IReadOnlyList<ComplianceIssue> ValidateCustomer(CustomerProfile customer)
    {
        var issues = new List<ComplianceIssue>();
        var values = customer.Values;

        if (values.TryGetValue(PersonTypeKey, out var type) && type is not (Individual or LegalEntity))
            issues.Add(new ComplianceIssue(PersonTypeKey, "Choose individual or legal entity"));

        if (IsPublicAdministration(values))
        {
            foreach (var (key, label) in new[]
            {
                (AccountingOfficeKey, "accounting office"),
                (ManagingBodyKey, "managing body"),
                (ProcessingUnitKey, "processing unit")
            })
            {
                if (!values.TryGetValue(key, out var code) || !Dir3Code().IsMatch(code))
                    issues.Add(new ComplianceIssue(key, $"A public administration needs the DIR3 code of its {label}"));
            }
        }

        if (ResolvePersonType(customer.FiscalId, values) == Individual && !HasValue(values, FirstSurnameKey))
            issues.Add(new ComplianceIssue(FirstSurnameKey, "An individual customer needs a first surname"));

        return issues;
    }

    public static bool IsPublicAdministration(IReadOnlyDictionary<string, string> customerValues) =>
        customerValues.TryGetValue(IsPublicAdministrationKey, out var flag) && flag == "true";

    /// <summary>The customer type chosen, or worked out from the tax id: NIF/NIE are individuals, CIF are entities.</summary>
    public static string ResolvePersonType(string? taxId, IReadOnlyDictionary<string, string> customerValues)
    {
        if (customerValues.TryGetValue(PersonTypeKey, out var chosen) && chosen is Individual or LegalEntity)
            return chosen;

        if (IsPublicAdministration(customerValues))
            return LegalEntity;

        var id = SpanishTaxId.Normalize(taxId);
        return id.Length > 0 && (char.IsAsciiDigit(id[0]) || id[0] is 'X' or 'Y' or 'Z' or 'K' or 'L' or 'M')
            ? Individual
            : LegalEntity;
    }

    /// <summary>Street, city, province and a five-digit postal code, for an address in Spain.</summary>
    public static IEnumerable<ComplianceIssue> ValidateSpanishAddress(Core.ValueObjects.Address address, string field, string subject)
    {
        if (!PostalCode().IsMatch(address.ZipCode.Trim()))
            yield return new ComplianceIssue(field, $"{subject}: a Spanish postal code has five digits");
        if (string.IsNullOrWhiteSpace(address.State))
            yield return new ComplianceIssue(field, $"{subject}: the province is required");
    }

    private static bool HasValue(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex PostalCode();

    [GeneratedRegex(@"^[A-Za-z0-9]{3,20}$")]
    private static partial Regex Dir3Code();
}
