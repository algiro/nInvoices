using nInvoices.Core.Interfaces;
using nInvoices.Core.Exceptions;

namespace nInvoices.Core.Entities;

/// <summary>
/// Subject and HTML body of the email that accompanies an invoice, for a specific customer or
/// shared by all of the user's customers (<see cref="CustomerId"/> is null). Both use the same
/// Scriban syntax as invoice templates. The active template is the one preselected when creating
/// an invoice email; at most one per customer (and one shared) is active, and a customer's own
/// wins over the shared one.
/// </summary>
public sealed class EmailTemplate : OwnedEntityBase, IScopedTemplate
{
    /// <summary>The customer the template belongs to; null for a template shared by all customers.</summary>
    public long? CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    // Navigation property (null for a shared template)
    public Customer? Customer { get; set; }

    public EmailTemplate()
    {
    }

    /// <param name="customerId">The customer, or null for a template shared by all customers.</param>
    public EmailTemplate(long? customerId, string name, string subject, string body) : this()
    {
        if (customerId <= 0)
            throw new ArgumentException("Customer ID must be positive", nameof(customerId)).WithText("errors.input.customerId");

        CustomerId = customerId;
        Update(name, subject, body);
    }

    public void Update(string name, string subject, string body)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(body);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be empty", nameof(name)).WithText("errors.input.templateName");
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Email subject cannot be empty", nameof(subject)).WithText("errors.input.emailSubject");
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Email body cannot be empty", nameof(body)).WithText("errors.input.emailBody");

        Name = name.Trim();
        Subject = subject;
        Body = body;
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
