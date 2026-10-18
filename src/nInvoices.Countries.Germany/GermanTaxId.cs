using System.Numerics;

namespace nInvoices.Countries.Germany;

/// <summary>
/// The German identifiers on an invoice: the Umsatzsteuer-Identifikationsnummer (USt-IdNr, "DE" and nine
/// digits, the last a check digit), the Steuernummer the Finanzamt gives, and the IBAN payments go to.
/// </summary>
public static class GermanTaxId
{
    /// <summary>Upper case, without spaces, dots, dashes or slashes.</summary>
    public static string Normalize(string? id) =>
        new string((id ?? string.Empty).Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();

    /// <summary>"DE" and nine digits, the ninth the ISO 7064 MOD 11,10 check digit of the others.</summary>
    public static bool IsValidVatId(string? vatId)
    {
        var id = Normalize(vatId);
        if (id.Length != 11 || !id.StartsWith("DE", StringComparison.Ordinal) || !id[2..].All(char.IsAsciiDigit))
            return false;

        var product = 10;
        for (var i = 2; i < 10; i++)
        {
            var sum = (id[i] - '0' + product) % 10;
            if (sum == 0)
                sum = 10;
            product = 2 * sum % 11;
        }
        var check = 11 - product;
        return (check == 10 ? 0 : check) == id[10] - '0';
    }

    /// <summary>
    /// A plausible Steuernummer: the 10 or 11 digits of a Land's format ("21/815/08150") or the 13 of the
    /// federal one. Its check digit depends on the Land and is not verified.
    /// </summary>
    public static bool IsPlausibleTaxNumber(string? taxNumber)
    {
        var digits = (taxNumber ?? string.Empty).Where(c => c is not (' ' or '/' or '-')).ToArray();
        return digits.All(char.IsAsciiDigit) && digits.Length is 10 or 11 or 13;
    }

    /// <summary>An IBAN of any country: its two check digits (ISO 13616, MOD 97) are right.</summary>
    public static bool IsValidIban(string? iban)
    {
        var id = Normalize(iban);
        if (id.Length is < 15 or > 34 || !char.IsAsciiLetter(id[0]) || !char.IsAsciiLetter(id[1]))
            return false;

        var rearranged = id[4..] + id[..4];
        var digits = string.Concat(rearranged.Select(c => char.IsAsciiDigit(c) ? c.ToString() : (c - 'A' + 10).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return BigInteger.Parse(digits, System.Globalization.CultureInfo.InvariantCulture) % 97 == 1;
    }
}
