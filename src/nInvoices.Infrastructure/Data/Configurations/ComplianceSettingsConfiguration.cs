using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;
using nInvoices.Infrastructure.Encryption;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class ComplianceSettingsConfiguration : IEntityTypeConfiguration<ComplianceSettings>
{
    public void Configure(EntityTypeBuilder<ComplianceSettings> builder)
    {
        builder.ToTable("ComplianceSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.CountryCode)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(s => s.IsEnabled)
            .IsRequired();

        builder.Property(s => s.LegalName)
            .IsEncrypted("ComplianceSettings.LegalName");

        builder.Property(s => s.TaxId)
            .IsEncrypted("ComplianceSettings.TaxId");

        builder.OwnsOne(s => s.Address, address =>
        {
            address.Property(a => a.Street).IsEncrypted("ComplianceSettings.Address.Street");
            address.Property(a => a.HouseNumber).IsEncrypted("ComplianceSettings.Address.HouseNumber");
            address.Property(a => a.City).IsEncrypted("ComplianceSettings.Address.City");
            address.Property(a => a.ZipCode).IsEncrypted("ComplianceSettings.Address.ZipCode");
            address.Property(a => a.Country).IsEncrypted("ComplianceSettings.Address.Country");
            address.Property(a => a.State).IsEncrypted("ComplianceSettings.Address.State");
        });

        // The country module own settings, as one JSON object
        builder.Property(s => s.Values)
            .HasJsonMapConversion();

        builder.Property(s => s.ProtectedCertificate);
        builder.Property(s => s.ProtectedCertificatePassword).HasMaxLength(4000);
        builder.Property(s => s.CertificateSubject).HasMaxLength(500);
        builder.Property(s => s.CertificateThumbprint).HasMaxLength(100);
        builder.Property(s => s.CertificateNotAfter);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt);

        // One record per user and country, created when the user first saves it
        builder.HasIndex(s => new { s.OwnerId, s.CountryCode })
            .IsUnique();
    }
}
