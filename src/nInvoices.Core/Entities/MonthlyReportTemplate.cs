using nInvoices.Core.Enums;
using nInvoices.Core.Exceptions;

using nInvoices.Core.Interfaces;

namespace nInvoices.Core.Entities;

/// <summary>
/// Represents a monthly report template for generating work day reports, for one customer or
/// shared by all of the user's customers (<see cref="CustomerId"/> is null).
/// Similar to InvoiceTemplate but specifically for monthly work reports.
/// </summary>
public sealed class MonthlyReportTemplate : OwnedEntityBase, IScopedTemplate
{
    /// <summary>The customer the template belongs to; null for a template shared by all customers.</summary>
    public long? CustomerId { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    // Navigation property (null for a shared template)
    public Customer? Customer { get; set; }

    public MonthlyReportTemplate()
    {
        IsActive = false; // Changed: new templates start as inactive
        InvoiceType = InvoiceType.Monthly; // Monthly reports are only for monthly invoices
    }

    /// <param name="customerId">The customer, or null for a template shared by all customers.</param>
    public MonthlyReportTemplate(
        long? customerId,
        string name,
        string content,
        InvoiceType invoiceType = InvoiceType.Monthly) : this()
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(content);

        if (customerId <= 0)
            throw new ArgumentException("Customer ID must be positive", nameof(customerId)).WithText("errors.input.customerId");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be empty", nameof(name)).WithText("errors.input.templateName");
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Template content cannot be empty", nameof(content)).WithText("errors.input.templateContent");
        if (invoiceType != InvoiceType.Monthly)
            throw new ArgumentException("Monthly report templates are only valid for Monthly invoice type", nameof(invoiceType)).WithText("errors.input.monthlyReportType");

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
            throw new ArgumentException("Template name cannot be empty", nameof(name)).WithText("errors.input.templateName");
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Template content cannot be empty", nameof(content)).WithText("errors.input.templateContent");

        Name = name;
        Content = content;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
