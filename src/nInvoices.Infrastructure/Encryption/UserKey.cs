using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace nInvoices.Infrastructure.Encryption;

/// <summary>
/// One user's data key, stored encrypted ("wrapped") by the server's master key.
/// Deleting the row makes everything encrypted with it unreadable, backups included.
/// </summary>
public sealed class UserKey
{
    /// <summary>Written at the start of every value the key encrypts.</summary>
    public Guid Id { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Which master key wrapped it (<see cref="MasterKeyRing.CurrentId"/>).</summary>
    public string MasterKeyId { get; set; } = string.Empty;

    /// <summary>nonce | tag | encrypted key.</summary>
    public byte[] WrappedKey { get; set; } = [];

    public DateTime CreatedAt { get; set; }
}

public sealed class UserKeyConfiguration : IEntityTypeConfiguration<UserKey>
{
    public void Configure(EntityTypeBuilder<UserKey> builder)
    {
        builder.ToTable("UserKeys");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).ValueGeneratedNever();
        builder.Property(k => k.OwnerId).IsRequired().HasMaxLength(255);
        builder.Property(k => k.MasterKeyId).IsRequired().HasMaxLength(16);
        builder.Property(k => k.WrappedKey).IsRequired();
        builder.Property(k => k.CreatedAt).IsRequired();
        builder.HasIndex(k => k.OwnerId).IsUnique();
    }
}
