using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data.Configurations;

public sealed class ImageAssetConfiguration : IEntityTypeConfiguration<ImageAsset>
{
    public void Configure(EntityTypeBuilder<ImageAsset> builder)
    {
        // Templates print an image by alias, so an alias names one image of its user. Per user:
        // two users may both have a "logo"
        builder.HasIndex(a => new { a.OwnerId, a.Alias })
            .IsUnique();
    }
}
