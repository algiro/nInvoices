using nInvoices.Core.ValueObjects;

namespace nInvoices.Core.Entities;

/// <summary>
/// Represents a customer (client) who receives invoices.
/// </summary>
public sealed class Customer : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string FiscalId { get; set; } = string.Empty;
    public Address Address { get; set; } = null!;

    // Navigation properties
    public ICollection<Rate> Rates { get; set; } = [];
    public ICollection<Tax> Taxes { get; set; } = [];
    public ICollection<InvoiceTemplate> Templates { get; set; } = [];
    public ICollection<Invoice> Invoices { get; set; } = [];

    public Customer()
    {
        CreatedAt = DateTime.UtcNow;
    }

    public Customer(string name, string fiscalId, Address address) : this()
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(fiscalId);
        ArgumentNullException.ThrowIfNull(address);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Customer name cannot be empty", nameof(name));
        if (string.IsNullOrWhiteSpace(fiscalId))
            throw new ArgumentException("Fiscal ID cannot be empty", nameof(fiscalId));

        Name = name;
        FiscalId = fiscalId;
        Address = address;
    }

    public void Update(string name, string fiscalId, Address address)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(fiscalId);
        ArgumentNullException.ThrowIfNull(address);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Customer name cannot be empty", nameof(name));
        if (string.IsNullOrWhiteSpace(fiscalId))
            throw new ArgumentException("Fiscal ID cannot be empty", nameof(fiscalId));

        Name = name;
        FiscalId = fiscalId;
        Address = address;
    }

    public void UpdateDetails(string name, string fiscalId, Address address)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(fiscalId);
        ArgumentNullException.ThrowIfNull(address);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Customer name cannot be empty", nameof(name));
        if (string.IsNullOrWhiteSpace(fiscalId))
            throw new ArgumentException("Fiscal ID cannot be empty", nameof(fiscalId));

        Name = name;
        FiscalId = fiscalId;
        Address = address;
        UpdatedAt = DateTime.UtcNow;
    }
}
