namespace nInvoices.Core.Compliance;

/// <summary>
/// The rules of one country invoicing regime. Modules are registered in the container and
/// found by <see cref="CountryCode"/>; nothing outside the module knows country specifics, so a
/// new country is a new module plus whatever its declared capabilities need.
/// </summary>
public interface ICountryComplianceModule
{
    /// <summary>ISO 3166-1 alpha-2 code, upper case.</summary>
    string CountryCode { get; }

    string DisplayName { get; }

    /// <summary>The concepts this country rules involve (and this module implements).</summary>
    IReadOnlySet<ComplianceCapability> Capabilities { get; }

    /// <summary>Country-specific settings of the issuer, beyond the common identity.</summary>
    IReadOnlyList<ComplianceField> Fields { get; }

    /// <summary>
    /// Extra data the country asks for on each customer (empty unless the module declares
    /// <see cref="ComplianceCapability.CustomerFiscalIdentity"/>).
    /// </summary>
    IReadOnlyList<ComplianceField> CustomerFields { get; }

    /// <summary>
    /// Checks the issuer against this country rules. Called when the user enables the regime;
    /// an empty result means the profile is acceptable.
    /// </summary>
    IReadOnlyList<ComplianceIssue> ValidateIssuer(IssuerProfile issuer);

    /// <summary>
    /// Checks that a customer's extra values are consistent with each other (for example, that a
    /// public body comes with its routing codes). Called when a customer is saved; the complete
    /// check for a given invoice is the e-invoice format's own validation.
    /// </summary>
    IReadOnlyList<ComplianceIssue> ValidateCustomer(CustomerProfile customer);
}
