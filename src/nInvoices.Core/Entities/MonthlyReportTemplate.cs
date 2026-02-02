using nInvoices.Core.Enums;

namespace nInvoices.Core.Entities;

/// <summary>
/// Represents a monthly report template for generating work day reports.
/// Similar to InvoiceTemplate but specifically for monthly work reports.
/// </summary>
public sealed class MonthlyReportTemplate : EntityBase
{
    public long CustomerId { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    // Navigation property
    public Customer Customer { get; set; } = null!;

    public MonthlyReportTemplate()
    {
        CreatedAt = DateTime.UtcNow;
        IsActive = false; // Changed: new templates start as inactive
        InvoiceType = InvoiceType.Monthly; // Monthly reports are only for monthly invoices
    }

    public MonthlyReportTemplate(
        long customerId,
        string name,
        string content,
        InvoiceType invoiceType = InvoiceType.Monthly) : this()
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(content);

        if (customerId <= 0)
            throw new ArgumentException("Customer ID must be positive", nameof(customerId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be empty", nameof(name));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Template content cannot be empty", nameof(content));
        if (invoiceType != InvoiceType.Monthly)
            throw new ArgumentException("Monthly report templates are only valid for Monthly invoice type", nameof(invoiceType));

        CustomerId = customerId;
        Name = name;
        Content = content;
        InvoiceType = invoiceType;
    }

    public void Update(string name, string content)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be empty", nameof(name));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Template content cannot be empty", nameof(content));

        Name = name;
        Content = content;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
