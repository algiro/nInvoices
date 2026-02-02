using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Core.Entities;

/// <summary>
/// Represents an invoice issued to a customer.
/// Aggregates worked days, expenses, and tax calculations.
/// </summary>
public sealed class Invoice : EntityBase
{
    public long CustomerId { get; set; }
    public InvoiceNumber Number { get; set; } = null!;
    public InvoiceType Type { get; set; }
    public InvoiceStatus Status { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly? DueDate { get; set; }
    
    public int? WorkedDays { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public long? MonthlyReportTemplateId { get; set; }

    public Money Subtotal { get; set; } = null!;
    public Money TotalExpenses { get; set; } = null!;
    public Money TotalTaxes { get; set; } = null!;
    public Money Total { get; set; } = null!;

    public string? RenderedContent { get; set; }
    public string? Notes { get; set; }

    // Navigation properties
    public Customer Customer { get; set; } = null!;
    public ICollection<Expense> Expenses { get; set; } = [];
    public ICollection<InvoiceTaxLine> TaxLines { get; set; } = [];

    public Invoice()
    {
        CreatedAt = DateTime.UtcNow;
        Status = InvoiceStatus.Draft;
    }

    public Invoice(
        long customerId,
        InvoiceNumber number,
        InvoiceType type,
        DateOnly issueDate,
        Money subtotal,
        string currency) : this()
    {
        ArgumentNullException.ThrowIfNull(number);
        ArgumentNullException.ThrowIfNull(subtotal);

        if (customerId <= 0)
            throw new ArgumentException("Customer ID must be positive", nameof(customerId));

        CustomerId = customerId;
        Number = number;
        Type = type;
        IssueDate = issueDate;
        Subtotal = subtotal;
        TotalExpenses = Money.Zero(currency);
        TotalTaxes = Money.Zero(currency);
        Total = subtotal;
    }

    public void SetMonthlyInvoiceDetails(int year, int month, int workedDays)
    {
        if (year < 2000 || year > 2100)
            throw new ArgumentException("Year must be between 2000 and 2100", nameof(year));
        if (month < 1 || month > 12)
            throw new ArgumentException("Month must be between 1 and 12", nameof(month));
        if (workedDays < 0)
            throw new ArgumentException("Worked days cannot be negative", nameof(workedDays));

        Year = year;
        Month = month;
        WorkedDays = workedDays;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddExpenses(Money expensesTotal)
    {
        ArgumentNullException.ThrowIfNull(expensesTotal);

        TotalExpenses = expensesTotal;
        RecalculateTotal();
    }

    public void AddTaxes(Money taxesTotal)
    {
        ArgumentNullException.ThrowIfNull(taxesTotal);

        TotalTaxes = taxesTotal;
        RecalculateTotal();
    }

    public void SetRenderedContent(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RenderedContent = content;
        UpdatedAt = DateTime.UtcNow;
    }

    public void FinalizeInvoice()
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("Only draft invoices can be finalized");

        Status = InvoiceStatus.Finalized;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsSent()
    {
        if (Status != InvoiceStatus.Finalized)
            throw new InvalidOperationException("Only finalized invoices can be marked as sent");

        Status = InvoiceStatus.Sent;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsPaid()
    {
        if (Status == InvoiceStatus.Cancelled)
            throw new InvalidOperationException("Cancelled invoices cannot be marked as paid");

        Status = InvoiceStatus.Paid;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == InvoiceStatus.Paid)
            throw new InvalidOperationException("Paid invoices cannot be cancelled");

        Status = InvoiceStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    private void RecalculateTotal()
    {
        Total = Subtotal + TotalExpenses + TotalTaxes;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Represents a single tax line item on an invoice.
/// </summary>
public sealed class InvoiceTaxLine
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public string TaxId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public Money BaseAmount { get; set; } = null!;
    public Money TaxAmount { get; set; } = null!;
    public int Order { get; set; }

    // Navigation property
    public Invoice Invoice { get; set; } = null!;
}
