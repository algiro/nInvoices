namespace nInvoices.Core.Compliance;

/// <summary>
/// The rules of one country's invoicing regime. Modules are registered in the container and
/// found by <see cref="CountryCode"/>; nothing outside the module knows country specifics, so a
/// new country is a new module plus whatever its declared capabilities need.
/// </summary>
public interface ICountryComplianceModule
{
    /// <summary>ISO 3166-1 alpha-2 code, upper case.</summary>
    string CountryCode { get; }

    string DisplayName { get; }

    /// <summary>The concepts this country's rules involve (and this module implements).</summary>
    IReadOnlySet<ComplianceCapability> Capabilities { get; }

    /// <summary>Country-specific settings, beyond the common issuer identity.</summary>
    IReadOnlyList<ComplianceField> Fields { get; }

    /// <summary>
    /// Checks the issuer against this country's rules. Called when the user enables the regime;
    /// an empty result means the profile is acceptable.
    /// </summary>
    IReadOnlyList<ComplianceIssue> ValidateIssuer(IssuerProfile issuer);
}
