using System.Text.RegularExpressions;

namespace nInvoices.Countries.UnitedKingdom;

/// <summary>
/// The United Kingdom's identifiers: the VAT registration number ("GB" and nine digits, the last two check
/// digits; twelve for a branch), the Unique Taxpayer Reference (UTR, ten digits) and the postcode.
/// </summary>
public static partial class UkTaxId
{
    /// <summary>Upper case, without spaces, dots or dashes, and without the "GB" prefix.</summary>
    public static string NormalizeVatNumber(string? vatNumber)
    {
        var id = new string((vatNumber ?? string.Empty).Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        return id.StartsWith("GB", StringComparison.Ordinal) ? id[2..] : id;
    }

    /// <summary>
    /// Nine digits (or twelve, a branch's), whose weighted sum (8 down to 2) plus the last two digits is a multiple
    /// of 97, or of 97 once 55 is added (the numbers issued since 2010).
    /// </summary>
    public static bool IsValidVatNumber(string? vatNumber)
    {
        var id = NormalizeVatNumber(vatNumber);
        if (id.Length is not (9 or 12) || !id.All(char.IsAsciiDigit))
            return false;

        var sum = 0;
        for (var i = 0; i < 7; i++)
            sum += (id[i] - '0') * (8 - i);
        var check = (id[7] - '0') * 10 + (id[8] - '0');
        return (sum + check) % 97 == 0 || (sum + check + 55) % 97 == 0;
    }

    /// <summary>Ten digits, as HMRC gives them to a Self Assessment taxpayer.</summary>
    public static bool IsPlausibleUtr(string? utr)
    {
        var digits = new string((utr ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());
        return digits.Length == 10 && digits.All(char.IsAsciiDigit);
    }

    /// <summary>A United Kingdom postcode, e.g. "SW1A 1AA" (the space optional).</summary>
    public static bool IsValidPostcode(string? postcode) => Postcode().IsMatch((postcode ?? string.Empty).Trim());

    [GeneratedRegex(@"^(GIR ?0AA|[A-Z]{1,2}\d[A-Z\d]? ?\d[A-Z]{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex Postcode();
}
