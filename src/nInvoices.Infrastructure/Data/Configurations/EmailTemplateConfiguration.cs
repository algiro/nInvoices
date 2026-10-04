using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;
using nInvoices.Infrastructure.Encryption;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("EmailTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Subject)
            .IsRequired()
            .IsEncrypted("EmailTemplate.Subject");

        builder.Property(t => t.Body)
            .IsRequired()
            .IsEncrypted("EmailTemplate.Body");

        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);

        builder.HasOne(t => t.Customer)
            .WithMany(c => c.EmailTemplates)
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.CustomerId);
    }
}
