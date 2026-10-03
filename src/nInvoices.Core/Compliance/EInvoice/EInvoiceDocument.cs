using nInvoices.Core.ValueObjects;

namespace nInvoices.Core.Compliance.EInvoice;

/// <summary>
/// A finalized invoice in a country-neutral, self-contained form: everything a structured
/// e-invoice format needs, already resolved (lines, taxes, parties). A format turns it into its
/// own file; nothing in it depends on how the invoice was produced.
/// </summary>
public sealed record EInvoiceDocument(
    string Number,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string Currency,
    IssuerProfile Issuer,
    EInvoiceParty Buyer,
    IReadOnlyList<EInvoiceLine> Lines,
    IReadOnlyList<EInvoiceTax> Taxes,
    // Total: what is payable, i.e. lines plus added taxes, minus withholdings
    decimal Total,
    string? Notes,
    /// <summary>Language of the invoice text (ISO 639-1, e.g. "es"); null if unknown.</summary>
    string? Language = null);

/// <param name="Values">The buyer's extra values for the format's country, without the country prefix.</param>
public sealed record EInvoiceParty(
    string Name,
    string TaxId,
    Address Address,
    IReadOnlyDictionary<string, string> Values);

/// <param name="UnitPrice">Price of one unit before taxes.</param>
/// <param name="Amount">Quantity times unit price, before taxes.</param>
public sealed record EInvoiceLine(string Description, decimal Quantity, decimal UnitPrice, decimal Amount);

public enum EInvoiceTaxKind
{
    /// <summary>Added to the price (VAT, sales tax...).</summary>
    Added,

    /// <summary>Held back from what the buyer pays and handed to the tax authority (income tax withholding).</summary>
    Withheld
}

/// <param name="Rate">Percentage, always positive; <see cref="Kind"/> says whether it is added or withheld.</param>
/// <param name="TaxableBase">Amount the tax is calculated on.</param>
/// <param name="Amount">Tax amount, always positive.</param>
public sealed record EInvoiceTax(
    EInvoiceTaxKind Kind,
    string Description,
    decimal Rate,
    decimal TaxableBase,
    decimal Amount);
