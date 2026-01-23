using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.HasKey(i => i.Id);

        // Configure InvoiceNumber value object
        builder.OwnsOne(i => i.Number, number =>
        {
            number.Property(n => n.Value)
                .HasColumnName("InvoiceNumber")
                .HasMaxLength(100)
                .IsRequired();
        });

        builder.Property(i => i.Type)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasDefaultValue(Core.Enums.InvoiceStatus.Draft);

        builder.Property(i => i.IssueDate)
            .IsRequired();

        builder.Property(i => i.DueDate);

        builder.Property(i => i.WorkedDays);
        builder.Property(i => i.Year);
        builder.Property(i => i.Month);

        // Configure Money value objects
        builder.OwnsOne(i => i.Subtotal, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("SubtotalAmount")
                .HasPrecision(18, 2)
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("SubtotalCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(i => i.TotalExpenses, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("TotalExpensesAmount")
                .HasPrecision(18, 2)
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("TotalExpensesCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(i => i.TotalTaxes, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("TotalTaxesAmount")
                .HasPrecision(18, 2)
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("TotalTaxesCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(i => i.Total, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("TotalAmount")
                .HasPrecision(18, 2)
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("TotalCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(i => i.RenderedContent)
            .HasMaxLength(int.MaxValue);

        builder.Property(i => i.Notes)
            .HasMaxLength(2000);

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt);

        // Relationships
        builder.HasMany(i => i.Expenses)
            .WithOne(e => e.Invoice)
            .HasForeignKey(e => e.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(i => i.TaxLines)
            .WithOne(t => t.Invoice)
            .HasForeignKey(t => t.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(i => i.IssueDate);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => new { i.CustomerId, i.Year, i.Month });
    }
}
