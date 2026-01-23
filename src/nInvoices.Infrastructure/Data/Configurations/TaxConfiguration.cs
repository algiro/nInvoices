using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class TaxConfiguration : IEntityTypeConfiguration<Tax>
{
    public void Configure(EntityTypeBuilder<Tax> builder)
    {
        builder.ToTable("Taxes");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TaxId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.HandlerId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Rate)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(t => t.ApplicationType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(t => t.Order)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(t => t.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);

        // Self-referencing for compound taxes
        builder.HasOne(t => t.AppliedToTax)
            .WithMany()
            .HasForeignKey(t => t.AppliedToTaxId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint: one tax ID per customer
        builder.HasIndex(t => new { t.CustomerId, t.TaxId })
            .IsUnique();
    }
}
