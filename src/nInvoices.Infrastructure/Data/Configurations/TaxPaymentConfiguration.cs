using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;
using nInvoices.Infrastructure.Encryption;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class TaxPaymentConfiguration : IEntityTypeConfiguration<TaxPayment>
{
    public void Configure(EntityTypeBuilder<TaxPayment> builder)
    {
        builder.ToTable("TaxPayments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Year).IsRequired();
        builder.Property(p => p.Period).IsRequired();
        builder.Property(p => p.Form).IsRequired().HasMaxLength(20);
        builder.Property(p => p.PaidOn).IsRequired();

        builder.OwnsOne(p => p.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("Amount")
                .IsEncrypted("TaxPayment.Amount")
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);

        // One payment per user, form and period
        builder.HasIndex(p => new { p.OwnerId, p.Year, p.Period, p.Form }).IsUnique();
    }
}
