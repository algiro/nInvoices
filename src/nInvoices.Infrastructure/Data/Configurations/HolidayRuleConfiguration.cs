using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class HolidayRuleConfiguration : IEntityTypeConfiguration<HolidayRule>
{
    public void Configure(EntityTypeBuilder<HolidayRule> builder)
    {
        builder.ToTable("HolidayRules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Kind)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.Weekday)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(r => r.IsActive)
            .IsRequired();

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt);

        builder.HasIndex(r => r.HolidayCalendarId);
    }
}
