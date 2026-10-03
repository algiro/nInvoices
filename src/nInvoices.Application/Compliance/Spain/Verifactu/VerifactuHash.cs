using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace nInvoices.Application.Compliance.Spain.Verifactu;

/// <summary>
/// The hash (huella) that chains Verifactu records, as AEAT specifies it ("Detalle de las
/// especificaciones técnicas para generación de la huella o hash de los registros de facturación",
/// v0.1.2): SHA-256 over the fields of the record in a fixed order, written as name=value pairs
/// joined with &amp;, in UTF-8, giving 64 upper-case hexadecimal characters. The first record of a
/// chain has no previous hash, which is written as an empty value.
/// </summary>
public static class VerifactuHash
{
    /// <summary>Hash of a record of an issued invoice (registro de alta).</summary>
    /// <param name="issueDate">DD-MM-YYYY.</param>
    /// <param name="totalTax">CuotaTotal, e.g. "12.35".</param>
    /// <param name="totalAmount">ImporteTotal, e.g. "123.45".</param>
    /// <param name="previousHash">Hash of the record before, or empty for the first record.</param>
    /// <param name="generatedAt">FechaHoraHusoGenRegistro, e.g. "2024-01-01T19:20:30+01:00".</param>
    public static string Issued(
        string issuerTaxId, string invoiceNumber, string issueDate, string invoiceType,
        string totalTax, string totalAmount, string previousHash, string generatedAt) =>
        Compute(
            ("IDEmisorFactura", issuerTaxId),
            ("NumSerieFactura", invoiceNumber),
            ("FechaExpedicionFactura", issueDate),
            ("TipoFactura", invoiceType),
            ("CuotaTotal", totalTax),
            ("ImporteTotal", totalAmount),
            ("Huella", previousHash),
            ("FechaHoraHusoGenRegistro", generatedAt));

    /// <summary>Hash of a record of a cancelled invoice (registro de anulación).</summary>
    public static string Cancelled(
        string issuerTaxId, string invoiceNumber, string issueDate, string previousHash, string generatedAt) =>
        Compute(
            ("IDEmisorFacturaAnulada", issuerTaxId),
            ("NumSerieFacturaAnulada", invoiceNumber),
            ("FechaExpedicionFacturaAnulada", issueDate),
            ("Huella", previousHash),
            ("FechaHoraHusoGenRegistro", generatedAt));

    /// <summary>An amount as the records write it: two decimals, a dot.</summary>
    public static string Amount(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>The date as the records write it: DD-MM-YYYY.</summary>
    public static string Date(DateOnly value) => value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    private static string Compute(params (string Name, string Value)[] fields)
    {
        // Each value is trimmed; an absent value leaves just "name="
        var text = string.Join("&", fields.Select(f => $"{f.Name}={(f.Value ?? "").Trim()}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
