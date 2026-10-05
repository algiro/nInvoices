using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Core.Entities;

/// <summary>
/// Represents an invoice issued to a customer.
/// Aggregates worked days, expenses, and tax calculations.
/// </summary>
public sealed class Invoice : OwnedEntityBase
{
    public long CustomerId { get; set; }

    /// <summary>Changed only through <see cref="Finalize"/>, <see cref="RenumberDraft"/> or <see cref="RestoreImported"/>.</summary>
    public InvoiceNumber Number { get; private set; } = null!;
    public InvoiceType Type { get; set; }

    /// <summary>Changed only through the lifecycle methods, as <see cref="InvoiceLifecycle"/> allows.</summary>
    public InvoiceStatus Status { get; private set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly? DueDate { get; set; }
    
    public int? WorkedDays { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public long? MonthlyReportTemplateId { get; set; }

    /// <summary>The rate the invoice was billed with; null when it was billed with the default one.</summary>
    public long? RateId { get; set; }

    /// <summary>Hours billed on a one-time invoice with an hourly rate; null otherwise.</summary>
    public decimal? Hours { get; set; }

    // Total is always Subtotal + TotalExpenses + TotalTaxes (except for an imported invoice, which keeps
    // the amounts it was exported with)
    public Money Subtotal { get; private set; } = null!;
    public Money TotalExpenses { get; private set; } = null!;
    public Money TotalTaxes { get; private set; } = null!;
    public Money Total { get; private set; } = null!;

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
    }

    /// <summary>
    /// Throws unless <paramref name="action"/> is allowed in the current status. Lets a caller check
    /// before doing work the action depends on, e.g. taking a number from the sequence.
    /// </summary>
    /// <exception cref="InvalidOperationException">The action is not allowed.</exception>
    public void EnsureAllowed(InvoiceAction action)
    {
        var reason = InvoiceLifecycle.WhyNot(Status, action);
        if (reason is not null)
            throw new InvalidOperationException($"Invoice {Number} cannot be {Describe(action)}: {char.ToLowerInvariant(reason[0])}{reason[1..]}.");
    }

    /// <summary>Issues the draft with <paramref name="number"/>, the one it keeps for good.</summary>
    public void Finalize(InvoiceNumber number)
    {
        ArgumentNullException.ThrowIfNull(number);
        Apply(InvoiceAction.Finalize);
        Number = number;
    }

    /// <summary>A draft shows the next number of the sequence, which changes as other invoices are finalized.</summary>
    public void RenumberDraft(InvoiceNumber number)
    {
        ArgumentNullException.ThrowIfNull(number);
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException($"Invoice {Number} is {Status}: only a draft can be renumbered.");

        Number = number;
    }

    public void MarkAsSent() => Apply(InvoiceAction.MarkAsSent);

    public void MarkAsPaid() => Apply(InvoiceAction.MarkAsPaid);

    public void Cancel() => Apply(InvoiceAction.Cancel);

    /// <summary>
    /// For import only: gives an invoice exported from nInvoices back its status and amounts as they
    /// were, without replaying its history. The amounts must be in the invoice's currency.
    /// </summary>
    public void RestoreImported(InvoiceStatus status, Money totalExpenses, Money totalTaxes, Money total)
    {
        ArgumentNullException.ThrowIfNull(totalExpenses);
        ArgumentNullException.ThrowIfNull(totalTaxes);
        ArgumentNullException.ThrowIfNull(total);
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown invoice status");
        if (new[] { totalExpenses, totalTaxes, total }.Any(m => m.Currency != Subtotal.Currency))
            throw new ArgumentException($"The amounts must be in the invoice currency, {Subtotal.Currency}");

        Status = status;
        TotalExpenses = totalExpenses;
        TotalTaxes = totalTaxes;
        Total = total;
    }

    private void Apply(InvoiceAction action)
    {
        EnsureAllowed(action);
        Status = InvoiceLifecycle.Target(action);
    }

    private static string Describe(InvoiceAction action) => action switch
    {
        InvoiceAction.Finalize => "finalized",
        InvoiceAction.MarkAsSent => "marked as sent",
        InvoiceAction.MarkAsPaid => "marked as paid",
        InvoiceAction.Cancel => "cancelled",
        InvoiceAction.Delete => "deleted",
        _ => action.ToString()
    };

    private void RecalculateTotal()
    {
        Total = Subtotal + TotalExpenses + TotalTaxes;
    }
}

/// <summary>
/// Represents a single tax line item on an invoice.
/// </summary>
public sealed class InvoiceTaxLine : IOwnedEntity
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public Money BaseAmount { get; set; } = null!;
    public Money TaxAmount { get; set; } = null!;
    public int Order { get; set; }

    // Navigation property
    public Invoice Invoice { get; set; } = null!;

    // Parameterless constructor for EF Core
    public InvoiceTaxLine()
    {
    }

    // Constructor for creating tax lines
    public InvoiceTaxLine(string taxId, string description, decimal rate, Money baseAmount, Money taxAmount, int order)
    {
        TaxId = taxId;
        Description = description;
        Rate = rate;
        BaseAmount = baseAmount;
        TaxAmount = taxAmount;
        Order = order;
    }
}
