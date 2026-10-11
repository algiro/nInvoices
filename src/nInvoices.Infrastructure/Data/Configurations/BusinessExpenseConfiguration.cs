using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;
using nInvoices.Infrastructure.Encryption;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class BusinessExpenseConfiguration : IEntityTypeConfiguration<BusinessExpense>
{
    public void Configure(EntityTypeBuilder<BusinessExpense> builder)
    {
        builder.ToTable("BusinessExpenses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Date).IsRequired();
        builder.Property(e => e.Description).IsRequired().IsEncrypted("BusinessExpense.Description");
        builder.Property(e => e.Supplier).IsEncrypted("BusinessExpense.Supplier");

        builder.OwnsOne(e => e.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("Amount")
                .IsEncrypted("BusinessExpense.Amount")
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(e => e.IndirectTax).IsRequired().IsEncrypted("BusinessExpense.IndirectTax");
        builder.Property(e => e.ReverseCharge).IsRequired();
        builder.Property(e => e.Category).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.DeductiblePercent).IsRequired().HasPrecision(5, 2);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        // Expenses are listed and added up by period
        builder.HasIndex(e => new { e.OwnerId, e.Date });
    }
}
