using System.Net.Mail;

namespace nInvoices.Application.Services.Email;

/// <summary>
/// Parsing and validation of the comma- or semicolon-separated address lists typed in the UI.
/// </summary>
public static class EmailAddresses
{
    private static readonly char[] Separators = [',', ';', ' ', '\n', '\r', '\t'];

    public static IReadOnlyList<string> Split(string? list) =>
        string.IsNullOrWhiteSpace(list)
            ? []
            : list.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// True for a plain address such as <c>name@example.com</c>; display-name forms are rejected
    /// so what is stored is exactly what is sent.
    /// </summary>
    public static bool IsValid(string address) =>
        MailAddress.TryCreate(address, out var parsed)
        && parsed.Address.Equals(address, StringComparison.OrdinalIgnoreCase)
        && parsed.Host.Contains('.');

    public static bool AreAllValid(string? list) => Split(list).All(IsValid);

    /// <summary>Normalizes a list to "a@x.com, b@y.com", or null when it is empty.</summary>
    public static string? Normalize(string? list)
    {
        var addresses = Split(list);
        return addresses.Count == 0 ? null : string.Join(", ", addresses);
    }
}
