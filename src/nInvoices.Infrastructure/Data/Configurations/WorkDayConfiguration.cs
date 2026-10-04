using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;
using nInvoices.Infrastructure.Encryption;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class WorkDayConfiguration : IEntityTypeConfiguration<WorkDay>
{
    public void Configure(EntityTypeBuilder<WorkDay> builder)
    {
        builder.ToTable("WorkDays");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Date)
            .IsRequired();

        builder.Property(w => w.DayType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(w => w.Notes)
            .IsEncrypted("WorkDay.Notes");

        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.UpdatedAt);

        builder.HasOne(w => w.Customer)
            .WithMany()
            .HasForeignKey(w => w.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint: one workday per customer per date
        builder.HasIndex(w => new { w.CustomerId, w.Date })
            .IsUnique();
    }
}
