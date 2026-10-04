using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;
using nInvoices.Infrastructure.Encryption;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class InvoiceTaxLineConfiguration : IEntityTypeConfiguration<InvoiceTaxLine>
{
    public void Configure(EntityTypeBuilder<InvoiceTaxLine> builder)
    {
        builder.ToTable("InvoiceTaxLines");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TaxId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Rate)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.ComplexProperty(t => t.BaseAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("BaseAmount")
                .IsEncrypted("InvoiceTaxLine.BaseAmount")
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("BaseCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.ComplexProperty(t => t.TaxAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("TaxAmount")
                .IsEncrypted("InvoiceTaxLine.TaxAmount")
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("TaxCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(t => t.Order).IsRequired();
    }
}
