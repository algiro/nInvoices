using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class HolidayCalendarConfiguration : IEntityTypeConfiguration<HolidayCalendar>
{
    public void Configure(EntityTypeBuilder<HolidayCalendar> builder)
    {
        builder.ToTable("HolidayCalendars");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CountryCode)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);

        builder.HasMany(c => c.Rules)
            .WithOne(r => r.HolidayCalendar)
            .HasForeignKey(r => r.HolidayCalendarId)
            .OnDelete(DeleteBehavior.Cascade);

        // One calendar per country
        builder.HasIndex(c => c.CountryCode)
            .IsUnique();
    }
}
