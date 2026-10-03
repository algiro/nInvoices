using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class VerifactuRecordConfiguration : IEntityTypeConfiguration<VerifactuRecord>
{
    public void Configure(EntityTypeBuilder<VerifactuRecord> builder)
    {
        builder.ToTable("VerifactuRecords");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Sequence).IsRequired();
        builder.Property(r => r.Kind).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.InvoiceId).IsRequired();
        builder.Property(r => r.IssuerTaxId).IsRequired().HasMaxLength(9);
        builder.Property(r => r.InvoiceNumber).IsRequired().HasMaxLength(60);
        builder.Property(r => r.IssueDate).IsRequired().HasMaxLength(10);
        builder.Property(r => r.InvoiceType).IsRequired().HasMaxLength(2);
        builder.Property(r => r.TotalTax).IsRequired().HasMaxLength(20);
        builder.Property(r => r.TotalAmount).IsRequired().HasMaxLength(20);
        builder.Property(r => r.PreviousHash).IsRequired().HasMaxLength(64);
        builder.Property(r => r.GeneratedAt).IsRequired().HasMaxLength(40);
        builder.Property(r => r.Hash).IsRequired().HasMaxLength(64);
        builder.Property(r => r.Xml).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt);

        // An invoice with records cannot be deleted: it is part of the chain
        builder.HasOne(r => r.Invoice)
            .WithMany()
            .HasForeignKey(r => r.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // One position in the chain per user: two records cannot claim the same one
        builder.HasIndex(r => new { r.OwnerId, r.Sequence })
            .IsUnique();

        builder.HasIndex(r => r.InvoiceId);
    }
}
