using nInvoices.Core.Enums;

namespace nInvoices.Core.Entities;

/// <summary>
/// Represents an invoice template for a specific customer and invoice type.
/// Templates contain placeholders like {{Variable}} that get replaced during rendering.
/// </summary>
public sealed class InvoiceTemplate : EntityBase
{
    public long CustomerId { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Format { get; set; } = "html";
    public int Version { get; set; }
    public bool IsActive { get; set; }

    // Navigation property
    public Customer Customer { get; set; } = null!;

    public InvoiceTemplate()
    {
        CreatedAt = DateTime.UtcNow;
        IsActive = false; // Changed: new templates start as inactive to allow multiple templates
        Version = 1;
    }

    public InvoiceTemplate(
        long customerId,
        InvoiceType invoiceType,
        string name,
        string content,
        string format = "html") : this()
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(format);

        if (customerId <= 0)
            throw new ArgumentException("Customer ID must be positive", nameof(customerId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be empty", nameof(name));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Template content cannot be empty", nameof(content));

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
            throw new ArgumentException("Template name cannot be empty", nameof(name));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Template content cannot be empty", nameof(content));

        Name = name;
        Content = content;
        Version++;
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
