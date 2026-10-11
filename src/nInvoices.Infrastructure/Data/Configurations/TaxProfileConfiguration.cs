using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class TaxProfileConfiguration : IEntityTypeConfiguration<TaxProfile>
{
    public void Configure(EntityTypeBuilder<TaxProfile> builder)
    {
        builder.ToTable("TaxProfiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.CountryCode).IsRequired().HasMaxLength(2);
        builder.Property(p => p.RegimeId).IsRequired().HasMaxLength(50);

        // The regime's settings, as one JSON object
        builder.Property(p => p.Values).HasJsonMapConversion();

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);

        // One per user
        builder.HasIndex(p => p.OwnerId).IsUnique();
    }
}
