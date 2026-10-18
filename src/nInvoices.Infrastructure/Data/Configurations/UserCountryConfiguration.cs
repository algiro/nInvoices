using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class UserCountryConfiguration : IEntityTypeConfiguration<UserCountry>
{
    public void Configure(EntityTypeBuilder<UserCountry> builder)
    {
        builder.ToTable("UserCountries");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CountryCode).IsRequired().HasMaxLength(2);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);

        // One per user
        builder.HasIndex(c => c.OwnerId).IsUnique();
    }
}
