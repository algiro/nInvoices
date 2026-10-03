namespace nInvoices.Core.Configuration;

/// <summary>
/// The Verifactu setup of this installation, from the "Compliance:Spain:Verifactu" section. The
/// producer is whoever supplies this software to the users (the one who signs its responsible
/// declaration); the records name it as the billing system. Until it is filled in, users cannot
/// turn Verifactu on.
/// </summary>
public sealed class VerifactuOptions
{
    public const string SectionName = "Compliance:Spain:Verifactu";

    /// <summary>"Test" (AEAT external test portal) or "Production". Unset: Verifactu is unavailable.</summary>
    public string? Environment { get; init; }

    /// <summary>Legal name of the producer of the software.</summary>
    public string? ProducerName { get; init; }

    /// <summary>NIF of the producer (9 characters).</summary>
    public string? ProducerTaxId { get; init; }

    /// <summary>Name of the billing system (at most 30 characters).</summary>
    public string SystemName { get; init; } = "nInvoices";

    /// <summary>Two-character id the producer gives this system.</summary>
    public string SystemId { get; init; } = "NI";

    /// <summary>Version of the software (at most 50 characters); empty to use the assembly version.</summary>
    public string? Version { get; init; }

    /// <summary>Identifies this installation (at most 100 characters).</summary>
    public string InstallationNumber { get; init; } = "1";

    /// <summary>Whether this installation serves several taxpayers (IndicadorMultiplesOT).</summary>
    public bool MultipleTaxpayers { get; init; }

    /// <summary>
    /// For development only: send to this address instead of the Tax Agency's, to try the flow against a mock
    /// service. Leave it unset in any real installation.
    /// </summary>
    public string? ServiceUrl { get; init; }

    public bool IsProduction => string.Equals(Environment, "Production", StringComparison.OrdinalIgnoreCase);

    public bool IsTest => string.Equals(Environment, "Test", StringComparison.OrdinalIgnoreCase);

    /// <summary>What is missing for Verifactu to be usable; empty when it is configured.</summary>
    public IReadOnlyList<string> Missing()
    {
        var missing = new List<string>();
        if (!IsProduction && !IsTest)
            missing.Add("Environment (Test or Production)");
        if (string.IsNullOrWhiteSpace(ProducerName))
            missing.Add("ProducerName");
        if (ProducerTaxId is not { Length: 9 })
            missing.Add("ProducerTaxId (9 characters)");
        if (string.IsNullOrWhiteSpace(SystemId) || SystemId.Length > 2)
            missing.Add("SystemId (at most 2 characters)");
        return missing;
    }
}
