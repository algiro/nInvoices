using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class EInvoiceSubmissionConfiguration : IEntityTypeConfiguration<EInvoiceSubmission>
{
    public void Configure(EntityTypeBuilder<EInvoiceSubmission> builder)
    {
        builder.ToTable("EInvoiceSubmissions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ChannelId).IsRequired().HasMaxLength(50);
        builder.Property(s => s.Environment).IsRequired().HasMaxLength(20);
        builder.Property(s => s.Reference).IsRequired().HasMaxLength(100);
        builder.Property(s => s.SubmittedAt).IsRequired();
        builder.Property(s => s.RegisteredAt);
        builder.Property(s => s.StatusCode).HasMaxLength(20);
        builder.Property(s => s.StatusName).HasMaxLength(200);
        builder.Property(s => s.CancellationStatus).HasMaxLength(200);
        builder.Property(s => s.CheckedAt);
        builder.Property(s => s.LastError).HasMaxLength(1600);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt);

        // A delivered invoice must not disappear with its file
        builder.HasOne(s => s.EInvoice)
            .WithMany()
            .HasForeignKey(s => s.InvoiceEInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // One delivery per file and channel
        builder.HasIndex(s => new { s.InvoiceEInvoiceId, s.ChannelId })
            .IsUnique();
    }
}
