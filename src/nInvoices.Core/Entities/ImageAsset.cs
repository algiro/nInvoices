namespace nInvoices.Core.Entities;

/// <summary>
/// Represents an uploaded image asset (logo, signature, stamp, etc.)
/// that can be referenced in invoice templates by alias.
/// </summary>
public sealed class ImageAsset : EntityBase
{
    public string Alias { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Base64Data { get; set; } = string.Empty;
    public long FileSize { get; set; }

    public ImageAsset()
    {
        CreatedAt = DateTime.UtcNow;
    }

    public ImageAsset(string alias, string fileName, string contentType, string base64Data, long fileSize) : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(base64Data);

        Alias = alias;
        FileName = fileName;
        ContentType = contentType;
        Base64Data = base64Data;
        FileSize = fileSize;
    }

    public void UpdateAlias(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        Alias = alias;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateImage(string fileName, string contentType, string base64Data, long fileSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(base64Data);

        FileName = fileName;
        ContentType = contentType;
        Base64Data = base64Data;
        FileSize = fileSize;
        UpdatedAt = DateTime.UtcNow;
    }
}
