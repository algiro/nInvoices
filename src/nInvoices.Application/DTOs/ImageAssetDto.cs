namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for image asset information.
/// </summary>
public sealed record ImageAssetDto(
    long Id,
    string Alias,
    string FileName,
    string ContentType,
    long FileSize,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Data transfer object for image asset with base64 data included.
/// Used when the actual image data is needed (e.g., for preview).
/// </summary>
public sealed record ImageAssetWithDataDto(
    long Id,
    string Alias,
    string FileName,
    string ContentType,
    string Base64Data,
    long FileSize,
    DateTime CreatedAt,
    DateTime UpdatedAt);
