using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;
using nInvoices.Infrastructure.Encryption;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class InvoiceEInvoiceConfiguration : IEntityTypeConfiguration<InvoiceEInvoice>
{
    public void Configure(EntityTypeBuilder<InvoiceEInvoice> builder)
    {
        builder.ToTable("InvoiceEInvoices");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CountryCode).IsRequired().HasMaxLength(2);
        builder.Property(e => e.FormatId).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Content).IsRequired().IsEncrypted("InvoiceEInvoice.Content");
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.FileExtension).IsRequired().HasMaxLength(10);
        builder.Property(e => e.Sha256).IsRequired().HasMaxLength(64);
        builder.Property(e => e.GeneratedAt).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        builder.HasOne(e => e.Invoice)
            .WithMany()
            .HasForeignKey(e => e.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // One file per invoice and format
        builder.HasIndex(e => new { e.InvoiceId, e.FormatId })
            .IsUnique();
    }
}
