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
    
    public CustomerTemplateModel Customer { get; init; } = null!;
    public List<LineItemTemplateModel> LineItems { get; init; } = [];
    public List<TaxTemplateModel> Taxes { get; init; } = [];
    
    public decimal Subtotal { get; init; }
    public decimal TotalTax { get; init; }
    public decimal Total { get; init; }
    
    // For monthly invoices
    public int? WorkedDays { get; init; }
    public int? MonthNumber { get; init; }
    public string? MonthDescription { get; init; }
    public decimal? MonthlyRate { get; init; }
    public decimal? TotalExpenses { get; init; }
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

public sealed record TaxTemplateModel
{
    public string Description { get; init; } = string.Empty;
    public decimal Rate { get; init; }
    public decimal Amount { get; init; }
}
