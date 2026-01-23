using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class InvoiceTemplateConfiguration : IEntityTypeConfiguration<InvoiceTemplate>
{
    public void Configure(EntityTypeBuilder<InvoiceTemplate> builder)
    {
        builder.ToTable("InvoiceTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.InvoiceType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(t => t.Content)
            .IsRequired()
            .HasMaxLength(int.MaxValue);

        builder.Property(t => t.Format)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("html");

        builder.Property(t => t.Version)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(t => t.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);

        // Unique constraint: one active template per customer + invoice type
        builder.HasIndex(t => new { t.CustomerId, t.InvoiceType, t.IsActive })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
