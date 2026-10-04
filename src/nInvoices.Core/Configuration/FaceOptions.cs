namespace nInvoices.Core.Configuration;

/// <summary>
/// The FACe setup of this installation (Spain's entry point for invoices to public administrations), from
/// the "Compliance:Spain:Face" section. Until the environment is set, users cannot send invoices to FACe.
/// </summary>
public sealed class FaceOptions
{
    public const string SectionName = "Compliance:Spain:Face";

    /// <summary>"Test" (FACe's staging environment) or "Production". Unset: sending to FACe is unavailable.</summary>
    public string? Environment { get; init; }

    /// <summary>
    /// Algorithm of the WS-Security signature FACe requires on every request: "Sha256" (default) or "Sha1".
    /// FACe's own documentation shows SHA-1; use it only if the Sha256 default is refused.
    /// </summary>
    public string SignatureAlgorithm { get; init; } = "Sha256";

    /// <summary>For development only: send to this address instead of FACe's, to try the flow against a mock service.</summary>
    public string? ServiceUrl { get; init; }

    public bool IsProduction => string.Equals(Environment, "Production", StringComparison.OrdinalIgnoreCase);

    public bool IsTest => string.Equals(Environment, "Test", StringComparison.OrdinalIgnoreCase);

    public bool UseSha1 => string.Equals(SignatureAlgorithm, "Sha1", StringComparison.OrdinalIgnoreCase);

    /// <summary>What is missing for FACe to be usable; empty when it is configured.</summary>
    public IReadOnlyList<string> Missing()
    {
        var missing = new List<string>();
        if (!IsProduction && !IsTest)
            missing.Add("Environment (Test or Production)");
        return missing;
    }
}
