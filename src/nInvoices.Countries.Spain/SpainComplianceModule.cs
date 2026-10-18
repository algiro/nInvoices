using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using nInvoices.Application.Localization;
using nInvoices.Core.Compliance;
using nInvoices.Core.Configuration;
using nInvoices.Countries.Spain.Verifactu;

namespace nInvoices.Countries.Spain;

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

    private readonly IAppText _text;
    private readonly VerifactuOptions _verifactu;

    public SpainComplianceModule(IAppText text, IOptions<VerifactuOptions>? verifactu = null)
    {
        _text = text;
        _verifactu = verifactu?.Value ?? new VerifactuOptions();
    }

    public string CountryCode => CountryCodeValue;

    public string DisplayName => _text.Translate("countries.ES");

    public IReadOnlySet<ComplianceCapability> Capabilities { get; } = new HashSet<ComplianceCapability>
    {
        ComplianceCapability.IssuerIdentity,
        ComplianceCapability.CustomerFiscalIdentity,
        ComplianceCapability.StructuredEInvoice,
        ComplianceCapability.ElectronicSignature,
        ComplianceCapability.TamperEvidentRecords,
        ComplianceCapability.VerificationMark
    };

    // Built on each read, in the language of the request
    public IReadOnlyList<ComplianceField> Fields =>
    [
        new ComplianceField(
            PersonTypeKey,
            T("compliance.spain.fields.personType.label"),
            ComplianceFieldType.Choice,
            Required: true,
            Help: T("compliance.spain.fields.personType.help"),
            Options: PersonTypes),
        new ComplianceField(FirstNameKey, T("compliance.spain.fields.firstName.label"), Help: T("compliance.spain.fields.firstName.help")),
        new ComplianceField(FirstSurnameKey, T("compliance.spain.fields.firstSurname.label"), Help: T("compliance.spain.fields.individualsOnly")),
        new ComplianceField(SecondSurnameKey, T("compliance.spain.fields.secondSurname.label"), Help: T("compliance.spain.fields.individualsOnlyIfAny")),
        new ComplianceField(
            VerifactuKey,
            "Verifactu",
            ComplianceFieldType.Boolean,
            Help: T("compliance.spain.fields.verifactu.help")),
        new ComplianceField(
            FaceEmailKey,
            T("compliance.spain.fields.faceEmail.label"),
            Help: T("compliance.spain.fields.faceEmail.help")),
        new ComplianceField(
            SealCertificateKey,
            T("compliance.spain.fields.sealCertificate.label"),
            ComplianceFieldType.Boolean,
            Help: T("compliance.spain.fields.sealCertificate.help"))
    ];

    public IReadOnlyList<ComplianceField> TaxFields =>
    [
        new ComplianceField(
            TaxTypeKey,
            T("compliance.spain.taxFields.taxType.label"),
            ComplianceFieldType.Choice,
            Help: T("compliance.spain.taxFields.taxType.help"),
            Options:
            [
                new ComplianceFieldOption(Iva, T("compliance.spain.taxFields.taxType.iva")),
                new ComplianceFieldOption(Igic, T("compliance.spain.taxFields.taxType.igic"))
            ]),
        new ComplianceField(
            OperationKey,
            T("compliance.spain.taxFields.operation.label"),
            ComplianceFieldType.Choice,
            Help: T("compliance.spain.taxFields.operation.help"),
            Options:
            [
                new ComplianceFieldOption("S1", T("compliance.spain.taxFields.operation.S1")),
                new ComplianceFieldOption("S2", T("compliance.spain.taxFields.operation.S2")),
                new ComplianceFieldOption("N2", T("compliance.spain.taxFields.operation.N2")),
                new ComplianceFieldOption("N1", T("compliance.spain.taxFields.operation.N1")),
                new ComplianceFieldOption("E1", T("compliance.spain.taxFields.operation.E1")),
                new ComplianceFieldOption("E2", T("compliance.spain.taxFields.operation.E2")),
                new ComplianceFieldOption("E3", T("compliance.spain.taxFields.operation.E3")),
                new ComplianceFieldOption("E4", T("compliance.spain.taxFields.operation.E4")),
                new ComplianceFieldOption("E5", T("compliance.spain.taxFields.operation.E5")),
                new ComplianceFieldOption("E6", T("compliance.spain.taxFields.operation.E6")),
                new ComplianceFieldOption("E7", T("compliance.spain.taxFields.operation.E7")),
                new ComplianceFieldOption("E8", T("compliance.spain.taxFields.operation.E8"))
            ],
            ZeroRateOnly: true)
    ];

    public IReadOnlyList<ComplianceField> CustomerFields =>
    [
        new ComplianceField(
            IsPublicAdministrationKey,
            T("compliance.spain.customerFields.isPublicAdministration.label"),
            ComplianceFieldType.Boolean,
            Help: T("compliance.spain.customerFields.isPublicAdministration.help")),
        new ComplianceField(AccountingOfficeKey, T("compliance.spain.customerFields.accountingOffice.label"), Help: T("compliance.spain.customerFields.dir3Help")),
        new ComplianceField(ManagingBodyKey, T("compliance.spain.customerFields.managingBody.label"), Help: T("compliance.spain.customerFields.dir3Help")),
        new ComplianceField(ProcessingUnitKey, T("compliance.spain.customerFields.processingUnit.label"), Help: T("compliance.spain.customerFields.dir3Help")),
        new ComplianceField(
            PersonTypeKey,
            T("compliance.spain.customerFields.personType.label"),
            ComplianceFieldType.Choice,
            Help: T("compliance.spain.customerFields.personType.help"),
            Options: PersonTypes),
        new ComplianceField(FirstSurnameKey, T("compliance.spain.fields.firstSurname.label"), Help: T("compliance.spain.customerFields.firstSurname.help")),
        new ComplianceField(SecondSurnameKey, T("compliance.spain.fields.secondSurname.label"), Help: T("compliance.spain.fields.individualsOnlyIfAny"))
    ];

    private IReadOnlyList<ComplianceFieldOption> PersonTypes =>
    [
        new ComplianceFieldOption(Individual, T("compliance.spain.personTypes.individual")),
        new ComplianceFieldOption(LegalEntity, T("compliance.spain.personTypes.legalEntity"))
    ];

    public IReadOnlyList<ComplianceIssue> ValidateIssuer(IssuerProfile issuer)
    {
        var issues = new List<ComplianceIssue>();

        if (string.IsNullOrWhiteSpace(issuer.LegalName))
            issues.Add(new ComplianceIssue("legalName", _text.Translate("compliance.common.legalNameRequired")));

        if (string.IsNullOrWhiteSpace(issuer.TaxId))
            issues.Add(new ComplianceIssue("taxId", _text.Translate("compliance.spain.nifRequired")));
        else if (!SpanishTaxId.IsValid(issuer.TaxId))
            issues.Add(new ComplianceIssue("taxId", _text.Translate("compliance.spain.nifInvalid")));

        if (issuer.Address is null)
            issues.Add(new ComplianceIssue("address", _text.Translate("compliance.common.addressRequired")));
        else
            issues.AddRange(ValidateSpanishAddress(issuer.Address, "address", _text.Translate("compliance.common.subjectAddress")));

        if (!issuer.Values.TryGetValue(PersonTypeKey, out var personType) || personType is not (Individual or LegalEntity))
        {
            issues.Add(new ComplianceIssue(PersonTypeKey, _text.Translate("compliance.common.chooseIssuerType")));
        }
        else if (personType == Individual)
        {
            if (!HasValue(issuer.Values, FirstNameKey))
                issues.Add(new ComplianceIssue(FirstNameKey, _text.Translate("compliance.common.firstNameRequired")));
            if (!HasValue(issuer.Values, FirstSurnameKey))
                issues.Add(new ComplianceIssue(FirstSurnameKey, _text.Translate("compliance.spain.firstSurnameRequired")));
        }

        if (issuer.Values.GetValueOrDefault(VerifactuKey) == "true")
        {
            var missing = _verifactu.Missing();
            if (missing.Count > 0)
                issues.Add(new ComplianceIssue(VerifactuKey, _text.Translate("compliance.spain.verifactuNotConfigured", ("missing", string.Join(", ", missing)), ("section", VerifactuOptions.SectionName))));
            if (!string.IsNullOrWhiteSpace(issuer.TaxId) && SpanishTaxId.Normalize(issuer.TaxId).Length != 9)
                issues.Add(new ComplianceIssue("taxId", _text.Translate("compliance.spain.verifactuNifLength")));
        }

        return issues;
    }

    public IReadOnlyList<ComplianceIssue> ValidateCustomer(CustomerProfile customer)
    {
        var issues = new List<ComplianceIssue>();
        var values = customer.Values;

        if (values.TryGetValue(PersonTypeKey, out var type) && type is not (Individual or LegalEntity))
            issues.Add(new ComplianceIssue(PersonTypeKey, _text.Translate("compliance.spain.chooseCustomerType")));

        if (IsPublicAdministration(values))
        {
            foreach (var (key, message) in new[]
            {
                (AccountingOfficeKey, "compliance.spain.dir3AccountingOffice"),
                (ManagingBodyKey, "compliance.spain.dir3ManagingBody"),
                (ProcessingUnitKey, "compliance.spain.dir3ProcessingUnit")
            })
            {
                if (!values.TryGetValue(key, out var code) || !Dir3Code().IsMatch(code))
                    issues.Add(new ComplianceIssue(key, _text.Translate(message)));
            }
        }

        if (ResolvePersonType(customer.FiscalId, values) == Individual && !HasValue(values, FirstSurnameKey))
            issues.Add(new ComplianceIssue(FirstSurnameKey, _text.Translate("compliance.spain.customerFirstSurnameRequired")));

        return issues;
    }

    /// <summary>Whether a tax is IGIC (Canary Islands) rather than IVA, from its Spanish values (<see cref="TaxFields"/>).</summary>
    public static bool IsIgic(IReadOnlyDictionary<string, string>? taxValues) =>
        taxValues?.GetValueOrDefault(TaxTypeKey) == Igic;

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
    public IEnumerable<ComplianceIssue> ValidateSpanishAddress(Core.ValueObjects.Address address, string field, string subject)
    {
        if (!PostalCode().IsMatch(address.ZipCode.Trim()))
            yield return new ComplianceIssue(field, _text.Translate("compliance.spain.postalCode", ("subject", subject)));
        if (string.IsNullOrWhiteSpace(address.State))
            yield return new ComplianceIssue(field, _text.Translate("compliance.spain.provinceRequired", ("subject", subject)));
    }

    private string T(string key) => _text.Translate(key);

    private static bool HasValue(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex PostalCode();

    [GeneratedRegex(@"^[A-Za-z0-9]{3,20}$")]
    private static partial Regex Dir3Code();
}
