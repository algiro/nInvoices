namespace nInvoices.Core.Configuration;

/// <summary>
/// Configuration settings for invoice generation.
/// Loaded from appsettings.json "Invoice" section.
/// </summary>
public sealed class InvoiceSettings
{
    public const string SectionName = "Invoice";

    /// <summary>
    /// Invoice number format pattern.
    /// Supports tokens: {YEAR}, {YEAR:yy}, {MONTH}, {MONTH:00}, {NUMBER}, {NUMBER:000}, {CUSTOMER}, {CUSTOMER:3}
    /// Example: "INV-{YEAR}-{MONTH:00}-{NUMBER:000}" produces "INV-2026-01-001"
    /// </summary>
    public string NumberFormat { get; init; } = "INV-{YEAR}-{MONTH:00}-{NUMBER:000}";
}
