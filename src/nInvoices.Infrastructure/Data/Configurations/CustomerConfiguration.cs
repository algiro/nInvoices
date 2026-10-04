using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Encryption;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);

        // Encrypted columns: lengths are checked by the validators, on the plain values
        builder.Property(c => c.Name)
            .IsRequired()
            .IsEncrypted("Customer.Name");

        builder.Property(c => c.FiscalId)
            .IsRequired()
            .IsEncrypted("Customer.FiscalId");

        builder.Property(c => c.Email)
            .IsEncrypted("Customer.Email");

        builder.Property(c => c.CcEmails)
            .IsEncrypted("Customer.CcEmails");

        builder.Property(c => c.HolidayCountry)
            .HasMaxLength(2);

        // The data country regimes ask for, as one JSON object keyed "<COUNTRY>.<field>"
        builder.Property(c => c.ComplianceValues)
            .HasJsonMapConversion()
            .HasDefaultValue(new Dictionary<string, string>());

        // Configure Address value object as owned entity
        builder.OwnsOne(c => c.Address, address =>
        {
            address.Property(a => a.Street).IsRequired().IsEncrypted("Customer.Address.Street");
            address.Property(a => a.HouseNumber).IsRequired().IsEncrypted("Customer.Address.HouseNumber");
            address.Property(a => a.City).IsRequired().IsEncrypted("Customer.Address.City");
            address.Property(a => a.ZipCode).IsRequired().IsEncrypted("Customer.Address.ZipCode");
            address.Property(a => a.Country).IsRequired().IsEncrypted("Customer.Address.Country");
            address.Property(a => a.State).IsEncrypted("Customer.Address.State");
        });

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt);

        // Relationships
        builder.HasMany(c => c.Rates)
            .WithOne(r => r.Customer)
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Taxes)
            .WithOne(t => t.Customer)
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Templates)
            .WithOne(t => t.Customer)
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Invoices)
            .WithOne(i => i.Customer)
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
