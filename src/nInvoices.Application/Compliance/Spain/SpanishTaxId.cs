namespace nInvoices.Application.Compliance.Spain;

/// <summary>Checks Spanish tax ids: NIF/DNI, NIE and CIF (check digit/letter included).</summary>
public static class SpanishTaxId
{
    private const string DniLetters = "TRWAGMYFPDXBNJZSQVHLCKE";
    private const string CifControlLetters = "JABCDEFGHI";

    /// <summary>Upper case, without spaces, hyphens or dots, and without a leading "ES".</summary>
    public static string Normalize(string? value)
    {
        var id = new string((value ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return id.Length == 11 && id.StartsWith("ES", StringComparison.Ordinal) ? id[2..] : id;
    }

    public static bool IsValid(string? value)
    {
        var id = Normalize(value);
        if (id.Length != 9)
            return false;

        var first = id[0];
        if (char.IsAsciiDigit(first))
            return IsValidDni(id);
        if (first is 'X' or 'Y' or 'Z')
            return IsValidNie(id);
        if (first is 'K' or 'L' or 'M')
            return IsValidKlm(id);
        return IsValidCif(id);
    }

    private static bool IsValidDni(string id) =>
        id[..8].All(char.IsAsciiDigit) && id[8] == DniLetters[int.Parse(id[..8]) % 23];

    private static bool IsValidNie(string id)
    {
        var prefix = id[0] - 'X'; // X=0, Y=1, Z=2
        var digits = id[1..8];
        return digits.All(char.IsAsciiDigit) && id[8] == DniLetters[int.Parse($"{prefix}{digits}") % 23];
    }

    // K, L and M: Spaniards under 14 and residents abroad; the letter is computed like a DNI's
    private static bool IsValidKlm(string id)
    {
        var digits = id[1..8];
        return digits.All(char.IsAsciiDigit) && id[8] == DniLetters[int.Parse(digits) % 23];
    }

    private static bool IsValidCif(string id)
    {
        if (!"ABCDEFGHJNPQRSUVW".Contains(id[0]))
            return false;

        var digits = id[1..8];
        if (!digits.All(char.IsAsciiDigit))
            return false;

        var sum = 0;
        for (var i = 0; i < 7; i++)
        {
            var d = digits[i] - '0';
            if (i % 2 == 0)
            {
                d *= 2;
                d = d / 10 + d % 10;
            }
            sum += d;
        }

        var control = (10 - sum % 10) % 10;
        var last = id[8];

        // Entity types that must use a letter, a digit, or accept either
        if ("PQRSNW".Contains(id[0]))
            return last == CifControlLetters[control];
        if ("ABEH".Contains(id[0]))
            return last == (char)('0' + control);
        return last == (char)('0' + control) || last == CifControlLetters[control];
    }
}
