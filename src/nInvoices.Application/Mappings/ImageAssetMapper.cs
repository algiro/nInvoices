using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Mappings;

/// <summary>
/// Maps <see cref="ImageAsset"/> entities to DTOs: without the image data for lists, with it for one asset.
/// </summary>
public static class ImageAssetMapper
{
    public static ImageAssetDto ToDto(ImageAsset asset) =>
        new(asset.Id, asset.Alias, asset.FileName, asset.ContentType, asset.FileSize,
            asset.CreatedAt, asset.UpdatedAt ?? asset.CreatedAt);

    public static ImageAssetWithDataDto ToDtoWithData(ImageAsset asset) =>
        new(asset.Id, asset.Alias, asset.FileName, asset.ContentType, asset.Base64Data, asset.FileSize,
            asset.CreatedAt, asset.UpdatedAt ?? asset.CreatedAt);
}
