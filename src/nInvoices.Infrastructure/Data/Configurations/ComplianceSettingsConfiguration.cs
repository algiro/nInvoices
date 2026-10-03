using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

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
            .HasMaxLength(200);

        builder.Property(s => s.TaxId)
            .HasMaxLength(50);

        builder.OwnsOne(s => s.Address, address =>
        {
            address.Property(a => a.Street).HasMaxLength(200);
            address.Property(a => a.HouseNumber).HasMaxLength(20);
            address.Property(a => a.City).HasMaxLength(100);
            address.Property(a => a.ZipCode).HasMaxLength(20);
            address.Property(a => a.Country).HasMaxLength(100);
            address.Property(a => a.State).HasMaxLength(100);
        });

        // The country module own settings, as one JSON object
        builder.Property(s => s.Values)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>(),
                new ValueComparer<Dictionary<string, string>>(
                    (a, b) => a != null && b != null && a.Count == b.Count && !a.Except(b).Any(),
                    v => v.Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key, kv.Value)),
                    v => new Dictionary<string, string>(v)))
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt);

        // One record per user and country, created when the user first saves it
        builder.HasIndex(s => new { s.OwnerId, s.CountryCode })
            .IsUnique();
    }
}
