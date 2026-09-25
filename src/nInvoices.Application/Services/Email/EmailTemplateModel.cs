using nInvoices.Application.Models;

namespace nInvoices.Application.Services.Email;

/// <summary>
/// Data available to email templates (subject and body). Built from a saved invoice, so it
/// carries totals and the period but not line items. Property names reach templates in
/// camelCase, e.g. <c>[[ invoiceNumber ]]</c>, <c>[[ customer.name ]]</c>.
/// </summary>
public sealed record EmailTemplateModel
{
    public string InvoiceNumber { get; init; } = string.Empty;
    public string InvoiceType { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public DateTime? DueDate { get; init; }
    public string Currency { get; init; } = string.Empty;

    public decimal Subtotal { get; init; }
    public decimal TotalTax { get; init; }
    public decimal TotalExpenses { get; init; }
    public decimal Total { get; init; }

    // Monthly invoices
    public int? WorkedDays { get; init; }
    public int? Year { get; init; }
    public int? MonthNumber { get; init; }

    /// <summary>Month name in the customer's language, e.g. "settembre".</summary>
    public string? MonthDescription { get; init; }

    public string Locale { get; init; } = "en-US";
    public EmailCustomerModel Customer { get; init; } = null!;

    /// <summary>The Gmail address the email is sent from, when Gmail is connected.</summary>
    public string SenderEmail { get; init; } = string.Empty;
}

public sealed record EmailCustomerModel
{
    public string Name { get; init; } = string.Empty;
    public string FiscalId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public AddressTemplateModel Address { get; init; } = null!;
}
