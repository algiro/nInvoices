using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;
using nInvoices.Infrastructure.Encryption;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class InvoiceEmailConfiguration : IEntityTypeConfiguration<InvoiceEmail>
{
    public void Configure(EntityTypeBuilder<InvoiceEmail> builder)
    {
        builder.ToTable("InvoiceEmails");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.From).IsRequired().IsEncrypted("InvoiceEmail.From");
        builder.Property(e => e.To).IsRequired().IsEncrypted("InvoiceEmail.To");
        builder.Property(e => e.Cc).IsEncrypted("InvoiceEmail.Cc");
        builder.Property(e => e.Subject).IsRequired().IsEncrypted("InvoiceEmail.Subject");
        builder.Property(e => e.Attachments).IsRequired().IsEncrypted("InvoiceEmail.Attachments");
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
