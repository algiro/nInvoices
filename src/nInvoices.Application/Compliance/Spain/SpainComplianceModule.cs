using System.Text.RegularExpressions;
using nInvoices.Core.Compliance;

namespace nInvoices.Application.Compliance.Spain;

/// <summary>
/// Spain: Facturae and FACe (invoices to public administrations), Verifactu and, later, B2B
/// e-invoicing. For now it only involves the issuer's identity; each later phase adds its
/// capability and fields here.
/// </summary>
public sealed partial class SpainComplianceModule : ICountryComplianceModule
{
    public const string PersonTypeKey = "personType";

    public string CountryCode => "ES";

    public string DisplayName => "Spain";

    public IReadOnlySet<ComplianceCapability> Capabilities { get; } =
        new HashSet<ComplianceCapability> { ComplianceCapability.IssuerIdentity };

    public IReadOnlyList<ComplianceField> Fields { get; } =
    [
        new ComplianceField(
            PersonTypeKey,
            "Issuer type",
            ComplianceFieldType.Choice,
            Required: true,
            Help: "Facturae distinguishes a natural person (autónomo) from a legal entity.",
            Options:
            [
                new ComplianceFieldOption("individual", "Individual (autónomo)"),
                new ComplianceFieldOption("legalEntity", "Legal entity (company)")
            ])
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
        else if (!PostalCode().IsMatch(issuer.Address.ZipCode.Trim()))
            issues.Add(new ComplianceIssue("address", "A Spanish postal code has five digits"));

        if (!issuer.Values.TryGetValue(PersonTypeKey, out var personType)
            || personType is not ("individual" or "legalEntity"))
            issues.Add(new ComplianceIssue(PersonTypeKey, "Choose whether the issuer is an individual or a legal entity"));

        return issues;
    }

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex PostalCode();
}
