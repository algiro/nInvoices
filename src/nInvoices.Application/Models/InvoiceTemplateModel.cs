using nInvoices.Application.Compliance.Spain.Verifactu;
using nInvoices.Core.Enums;

namespace nInvoices.Application.Models;

/// <summary>
/// Model for rendering invoice templates.
/// All property names are in PascalCase (converted to camelCase by renderer).
/// </summary>
public sealed record InvoiceTemplateModel
{
    public string InvoiceNumber { get; init; } = string.Empty;
    public string InvoiceType { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public DateTime? DueDate { get; init; }
    public string Currency { get; init; } = string.Empty;

    /// <summary>The customer's locale (e.g. "it-IT"), for FormatDate/FormatCurrency/FormatDecimal and the Localize functions.</summary>
    public string Locale { get; init; } = "en-US";
    
    public CustomerTemplateModel Customer { get; init; } = null!;
    public List<LineItemTemplateModel> LineItems { get; init; } = [];
    public List<TaxTemplateModel> Taxes { get; init; } = [];

    /// <summary>Per-project totals for the billed period (empty when no projects were tracked).</summary>
    public List<ProjectSummaryTemplateModel> ProjectSummary { get; init; } = [];
    
    public decimal Subtotal { get; init; }
    public decimal TotalTax { get; init; }
    public decimal Total { get; init; }
    
    // For monthly invoices
    public int? WorkedDays { get; init; }
    public int? MonthNumber { get; init; }
    public string? MonthDescription { get; init; }
    public decimal? MonthlyRate { get; init; }
    public decimal? TotalExpenses { get; init; }
    public List<WorkedDayTemplateModel> WorkedDayItems { get; init; } = [];

    /// <summary>
    /// What country rules add to the invoice document, by name (e.g. <c>compliance.verifactu</c> with the
    /// QR code). Every entry a template may refer to is always there, empty until the rules apply to the
    /// invoice, so a template can print its parts without guarding them (a template engine fails on a
    /// member of something that is missing).
    /// </summary>
    public Dictionary<string, object> Compliance { get; init; } = new()
    {
        [VerifactuTemplateContributor.Key] = new VerifactuTemplateModel()
    };
}

public sealed record CustomerTemplateModel
{
    public string Name { get; init; } = string.Empty;
    public string FiscalId { get; init; } = string.Empty;
    public AddressTemplateModel Address { get; init; } = null!;
}

public sealed record AddressTemplateModel
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
}

public sealed record LineItemTemplateModel
{
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal Rate { get; init; }
    public decimal Amount { get; init; }
}

public sealed record WorkedDayTemplateModel
{
    public string Date { get; init; } = string.Empty;
    public decimal Hours { get; init; }
}

public sealed record TaxTemplateModel
{
    public string Description { get; init; } = string.Empty;
    public decimal Rate { get; init; }
    public decimal Amount { get; init; }
}

/// <summary>
/// Aggregated time (and, where applicable, billed amount) for a single project
/// over the invoiced / reported month.
/// </summary>
public sealed record ProjectSummaryTemplateModel
{
    public string Name { get; init; } = string.Empty;
    public decimal TotalHours { get; init; }
    public int WorkedDays { get; init; }
    public decimal? Amount { get; init; }
}
