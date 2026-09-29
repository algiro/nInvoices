using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for MonthlyReportTemplate entity.
/// Defines table structure, relationships, and constraints.
/// </summary>
public sealed class MonthlyReportTemplateConfiguration : IEntityTypeConfiguration<MonthlyReportTemplate>
{
    public void Configure(EntityTypeBuilder<MonthlyReportTemplate> builder)
    {
        builder.ToTable("MonthlyReportTemplates");

        builder.HasKey(t => t.Id);
        
        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd();

        // No customer = a template shared by all the user's customers
        builder.Property(t => t.CustomerId);

        builder.Property(t => t.InvoiceType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Content)
            .IsRequired();

        builder.Property(t => t.IsActive)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.UpdatedAt);

        // Relationship with Customer (optional: shared templates have none)
        builder.HasOne(t => t.Customer)
            .WithMany()
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for faster queries (not unique - allows multiple templates)
        builder.HasIndex(t => new { t.CustomerId, t.InvoiceType, t.IsActive });
    }
}
