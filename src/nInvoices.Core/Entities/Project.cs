namespace nInvoices.Core.Entities;

/// <summary>
/// Represents a project a freelancer works on for a specific customer.
/// Projects are referenced by work day allocations to break down time per project.
/// </summary>
public sealed class Project : EntityBase
{
    public long CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    // Navigation property
    public Customer Customer { get; set; } = null!;

    public Project()
    {
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
    }

    public Project(long customerId, string name) : this()
    {
        ArgumentNullException.ThrowIfNull(name);

        if (customerId <= 0)
            throw new ArgumentException("Customer ID must be positive", nameof(customerId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name cannot be empty", nameof(name));

        CustomerId = customerId;
        Name = name.Trim();
    }

    public void Rename(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name cannot be empty", nameof(name));

        Name = name.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
