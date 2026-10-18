namespace nInvoices.Countries.Italy;

/// <summary>
/// The Italian tax identifiers: the Partita IVA (eleven digits, the last a check digit) and the codice
/// fiscale (sixteen characters for a person, the last a check letter; eleven digits for a company, as its
/// Partita IVA).
/// </summary>
public static class ItalianTaxId
{
    // The value of each character in an odd position (1st, 3rd...) of a personal codice fiscale: digits count as A..J
    private static readonly int[] OddValues = [1, 0, 5, 7, 9, 13, 15, 17, 19, 21, 2, 4, 18, 20, 11, 3, 6, 8, 12, 14, 16, 10, 22, 25, 24, 23];

    // Where a personal codice fiscale has digits; a duplicate code (omocodia) replaces them with these letters
    private static readonly int[] DigitPositions = [6, 7, 9, 10, 12, 13, 14];
    private const string OmocodiaLetters = "LMNPQRSTUV";

    /// <summary>Upper case, without spaces, dots, dashes or an "IT" prefix.</summary>
    public static string Normalize(string? taxId)
    {
        var id = new string((taxId ?? string.Empty).Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        return id.Length == 13 && id.StartsWith("IT", StringComparison.Ordinal) ? id[2..] : id;
    }

    public static bool IsValidPartitaIva(string? taxId)
    {
        var id = Normalize(taxId);
        if (id.Length != 11 || !id.All(char.IsAsciiDigit))
            return false;

        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var digit = id[i] - '0';
            if (i % 2 == 1)
            {
                digit *= 2;
                if (digit > 9)
                    digit -= 9;
            }
            sum += digit;
        }
        return (10 - sum % 10) % 10 == id[10] - '0';
    }

    /// <summary>A person's sixteen-character code or a company's eleven digits.</summary>
    public static bool IsValidCodiceFiscale(string? code)
    {
        var id = new string((code ?? string.Empty).Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        if (id.Length == 11)
            return IsValidPartitaIva(id);
        if (id.Length != 16 || !id.All(char.IsAsciiLetterOrDigit))
            return false;

        for (var i = 0; i < 15; i++)
        {
            var mustBeDigit = DigitPositions.Contains(i);
            if (mustBeDigit ? !(char.IsAsciiDigit(id[i]) || OmocodiaLetters.Contains(id[i])) : !char.IsAsciiLetterUpper(id[i]))
                return false;
        }
        if (!char.IsAsciiLetterUpper(id[15]))
            return false;

        var sum = 0;
        for (var i = 0; i < 15; i++)
        {
            var index = char.IsAsciiDigit(id[i]) ? id[i] - '0' : id[i] - 'A';
            // Positions are counted from 1: the 1st, 3rd... are odd
            sum += i % 2 == 0 ? OddValues[index] : index;
        }
        return (char)('A' + sum % 26) == id[15];
    }
}
