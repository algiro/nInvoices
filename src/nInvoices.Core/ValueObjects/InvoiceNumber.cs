using System.Globalization;
using System.Text.RegularExpressions;

namespace nInvoices.Core.ValueObjects;

/// <summary>
/// Represents an invoice number with support for custom formatting.
/// Format tokens: {YEAR}, {MONTH}, {NUMBER}, {CUSTOMER}
/// Example: "INV-{YEAR}-{NUMBER:000}" -> "INV-2026-001"
/// </summary>
public sealed record InvoiceNumber
{
    public string Value { get; init; }

    public InvoiceNumber(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Invoice number cannot be empty", nameof(value));

        Value = value;
    }

    public static InvoiceNumber Generate(
        string pattern,
        int sequenceNumber,
        DateTime date,
        string? customerCode = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var result = pattern;

        result = ReplaceToken(result, "YEAR", date.Year.ToString(CultureInfo.InvariantCulture));
        result = ReplaceToken(result, "YEAR:yy", date.ToString("yy", CultureInfo.InvariantCulture));
        result = ReplaceToken(result, "MONTH:00", date.Month.ToString("00", CultureInfo.InvariantCulture));
        result = ReplaceToken(result, "MONTH", date.Month.ToString(CultureInfo.InvariantCulture));

        if (customerCode is not null)
        {
            result = ReplaceToken(result, "CUSTOMER:3", customerCode[..Math.Min(3, customerCode.Length)].ToUpperInvariant());
            result = ReplaceToken(result, "CUSTOMER", customerCode.ToUpperInvariant());
        }

        var numberMatch = Regex.Match(result, @"\{NUMBER:([0]+)\}");
        if (numberMatch.Success)
        {
            var format = numberMatch.Groups[1].Value;
            var formattedNumber = sequenceNumber.ToString(new string('0', format.Length), CultureInfo.InvariantCulture);
            result = result.Replace(numberMatch.Value, formattedNumber);
        }
        else
        {
            result = ReplaceToken(result, "NUMBER", sequenceNumber.ToString(CultureInfo.InvariantCulture));
        }

        return new InvoiceNumber(result);
    }

    private static string ReplaceToken(string input, string token, string value) =>
        input.Replace($"{{{token}}}", value);

    public static bool ValidatePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        var validTokens = new[] { "YEAR", "YEAR:yy", "MONTH", "MONTH:00", "NUMBER", "CUSTOMER", "CUSTOMER:3" };
        var numberPattern = @"\{NUMBER:[0]+\}";

        var hasValidToken = validTokens.Any(token => pattern.Contains($"{{{token}}}")) ||
                           Regex.IsMatch(pattern, numberPattern);

        return hasValidToken;
    }

    public static string GetPreview(string pattern) =>
        Generate(pattern, 1, DateTime.Now, "CUST").Value;

    public override string ToString() => Value;

    public static implicit operator string(InvoiceNumber number) => number.Value;
}
