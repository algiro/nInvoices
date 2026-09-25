using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class InvoiceEmailConfiguration : IEntityTypeConfiguration<InvoiceEmail>
{
    public void Configure(EntityTypeBuilder<InvoiceEmail> builder)
    {
        builder.ToTable("InvoiceEmails");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.From).IsRequired().HasMaxLength(320);
        builder.Property(e => e.To).IsRequired().HasMaxLength(2000);
        builder.Property(e => e.Cc).HasMaxLength(2000);
        builder.Property(e => e.Subject).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.Attachments).IsRequired().HasMaxLength(2000);
        builder.Property(e => e.GmailDraftId).IsRequired().HasMaxLength(200);
        builder.Property(e => e.GmailMessageId).IsRequired().HasMaxLength(200);
        builder.Property(e => e.RfcMessageId).IsRequired().HasMaxLength(500);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        builder.HasOne(e => e.Invoice)
            .WithMany()
            .HasForeignKey(e => e.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.InvoiceId);
    }
}
