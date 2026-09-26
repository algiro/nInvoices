using nInvoices.Core.ValueObjects;

namespace nInvoices.Core.Entities;

/// <summary>
/// Represents a customer (client) who receives invoices.
/// </summary>
public sealed class Customer : OwnedEntityBase
{
    public string Name { get; set; } = string.Empty;
    public string FiscalId { get; set; } = string.Empty;
    public string Locale { get; set; } = "en-US";
    public Address Address { get; set; } = null!;

    /// <summary>Default recipient of invoice emails.</summary>
    public string? Email { get; set; }

    /// <summary>Comma-separated addresses copied on invoice emails.</summary>
    public string? CcEmails { get; set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 code of the country whose public holidays apply to this customer's
    /// time sheets; null to use the country of the address.
    /// </summary>
    public string? HolidayCountry { get; private set; }

    // Navigation properties
    public ICollection<Rate> Rates { get; set; } = [];
    public ICollection<Tax> Taxes { get; set; } = [];
    public ICollection<InvoiceTemplate> Templates { get; set; } = [];
    public ICollection<Invoice> Invoices { get; set; } = [];
    public ICollection<Project> Projects { get; set; } = [];
    public ICollection<EmailTemplate> EmailTemplates { get; set; } = [];

    public Customer()
    {
        CreatedAt = DateTime.UtcNow;
    }

    public Customer(string name, string fiscalId, Address address, string locale = "en-US") : this()
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
        Locale = locale;
    }

    /// <param name="countryCode">A two-letter country code, or null/blank to follow the address.</param>
    public void SetHolidayCountry(string? countryCode)
    {
        HolidayCountry = string.IsNullOrWhiteSpace(countryCode)
            ? null
            : HolidayCalendar.NormalizeCountryCode(countryCode);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string name, string fiscalId, Address address, string locale = "en-US")
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
        Locale = locale;
    }

    public void UpdateDetails(string name, string fiscalId, Address address, string locale = "en-US")
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
        Locale = locale;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetContact(string? email, string? ccEmails)
    {
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        CcEmails = string.IsNullOrWhiteSpace(ccEmails) ? null : ccEmails.Trim();
    }
}
