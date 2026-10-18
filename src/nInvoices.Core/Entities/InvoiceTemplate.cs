using nInvoices.Core.Enums;
using nInvoices.Core.Exceptions;

using nInvoices.Core.Interfaces;

namespace nInvoices.Core.Entities;

/// <summary>
/// Represents an invoice template for an invoice type, either for one customer or shared by all
/// of the user's customers (<see cref="CustomerId"/> is null). A customer's own active template
/// wins over the shared active one. Templates contain placeholders like {{Variable}} that get
/// replaced during rendering.
/// </summary>
public sealed class InvoiceTemplate : OwnedEntityBase, IScopedTemplate
{
    /// <summary>The customer the template belongs to; null for a template shared by all customers.</summary>
    public long? CustomerId { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Format { get; set; } = "html";
    public int Version { get; set; }
    public bool IsActive { get; set; }

    // Navigation property (null for a shared template)
    public Customer? Customer { get; set; }

    public InvoiceTemplate()
    {
        IsActive = false; // Changed: new templates start as inactive to allow multiple templates
        Version = 1;
    }

    /// <param name="customerId">The customer, or null for a template shared by all customers.</param>
    public InvoiceTemplate(
        long? customerId,
        InvoiceType invoiceType,
        string name,
        string content,
        string format = "html") : this()
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(format);

        if (customerId <= 0)
            throw new ArgumentException("Customer ID must be positive", nameof(customerId)).WithText("errors.input.customerId");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be empty", nameof(name)).WithText("errors.input.templateName");
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Template content cannot be empty", nameof(content)).WithText("errors.input.templateContent");

        CustomerId = customerId;
        InvoiceType = invoiceType;
        Name = name;
        Content = content;
        Format = format.ToLowerInvariant();
    }

    public void UpdateContent(string name, string content)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be empty", nameof(name)).WithText("errors.input.templateName");
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Template content cannot be empty", nameof(content)).WithText("errors.input.templateContent");

        Name = name;
        Content = content;
        Version++;
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
