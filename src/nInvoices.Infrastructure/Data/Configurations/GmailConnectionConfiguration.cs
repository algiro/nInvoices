using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class GmailConnectionConfiguration : IEntityTypeConfiguration<GmailConnection>
{
    public void Configure(EntityTypeBuilder<GmailConnection> builder)
    {
        builder.ToTable("GmailConnections");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.EmailAddress)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(c => c.EncryptedRefreshToken)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(c => c.Scopes)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(c => c.ConnectedAt).IsRequired();
        builder.Property(c => c.LastUsedAt);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);

        builder.HasIndex(c => c.UserId).IsUnique();
    }
}
