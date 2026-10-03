using System.Text.RegularExpressions;
using nInvoices.Core.Compliance;

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

    public const string Individual = "individual";
    public const string LegalEntity = "legalEntity";

    private static readonly IReadOnlyList<ComplianceFieldOption> PersonTypes =
    [
        new ComplianceFieldOption(Individual, "Individual (autónomo)"),
        new ComplianceFieldOption(LegalEntity, "Legal entity (company)")
    ];

    public string CountryCode => CountryCodeValue;

    public string DisplayName => "Spain";

    public IReadOnlySet<ComplianceCapability> Capabilities { get; } = new HashSet<ComplianceCapability>
    {
        ComplianceCapability.IssuerIdentity,
        ComplianceCapability.CustomerFiscalIdentity,
        ComplianceCapability.StructuredEInvoice,
        ComplianceCapability.ElectronicSignature
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
        new ComplianceField(SecondSurnameKey, "Second surname", Help: "Individuals only, if any.")
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
