namespace nInvoices.Core.Entities;

/// <summary>
/// Subject and HTML body of the email that accompanies an invoice, for a specific customer.
/// Both use the same Scriban syntax as invoice templates. The active template is the one
/// preselected when creating an invoice email; at most one per customer is active.
/// </summary>
public sealed class EmailTemplate : EntityBase
{
    public long CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    // Navigation property
    public Customer Customer { get; set; } = null!;

    public EmailTemplate()
    {
        CreatedAt = DateTime.UtcNow;
    }

    public EmailTemplate(long customerId, string name, string subject, string body) : this()
    {
        if (customerId <= 0)
            throw new ArgumentException("Customer ID must be positive", nameof(customerId));

        CustomerId = customerId;
        Update(name, subject, body);
    }

    public void Update(string name, string subject, string body)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(body);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be empty", nameof(name));
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Email subject cannot be empty", nameof(subject));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Email body cannot be empty", nameof(body));

        Name = name.Trim();
        Subject = subject;
        Body = body;
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
