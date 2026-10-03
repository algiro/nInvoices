using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class VerifactuSubmissionConfiguration : IEntityTypeConfiguration<VerifactuSubmission>
{
    public void Configure(EntityTypeBuilder<VerifactuSubmission> builder)
    {
        builder.ToTable("VerifactuSubmissions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.RecordId).IsRequired();
        builder.Property(s => s.Sequence).IsRequired();
        builder.Property(s => s.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Attempts).IsRequired();
        builder.Property(s => s.LastAttemptAt);
        builder.Property(s => s.NextAttemptAt).IsRequired();
        builder.Property(s => s.AnsweredAt);
        builder.Property(s => s.Csv).HasMaxLength(50);
        builder.Property(s => s.ErrorCode).HasMaxLength(20);
        builder.Property(s => s.Message).HasMaxLength(1600);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt);

        builder.HasOne(s => s.Record)
            .WithMany()
            .HasForeignKey(s => s.RecordId)
            .OnDelete(DeleteBehavior.Restrict);

        // One submission per record
        builder.HasIndex(s => s.RecordId).IsUnique();

        // The worker looks for what is waiting to be sent, in chain order
        builder.HasIndex(s => new { s.Status, s.NextAttemptAt });
    }
}
