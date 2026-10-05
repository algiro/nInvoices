using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;
using nInvoices.Infrastructure.Encryption;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class AccessRequestConfiguration : IEntityTypeConfiguration<AccessRequest>
{
    public void Configure(EntityTypeBuilder<AccessRequest> builder)
    {
        builder.ToTable("AccessRequests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Email).IsEncrypted("AccessRequest.Email");
        builder.Property(r => r.Name).IsEncrypted("AccessRequest.Name");
        builder.Property(r => r.NotifiedAt);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt);

        // One per user
        builder.HasIndex(r => r.OwnerId).IsUnique();
    }
}
