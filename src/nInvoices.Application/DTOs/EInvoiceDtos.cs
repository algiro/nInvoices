namespace nInvoices.Application.DTOs;

/// <summary>
/// One e-invoice format that applies to an invoice: whether the rules require it for this customer,
/// and whether a file was generated.
/// </summary>
public sealed record InvoiceEInvoiceDto(
    string CountryCode,
    string CountryName,
    string FormatId,
    string FormatName,
    bool IsMandatory,
    bool IsGenerated,
    DateTime? GeneratedAt,
    string? FileName,
    string? Sha256);

/// <summary>The outcome of generating one format; <see cref="Issues"/> says why it could not be.</summary>
public sealed record EInvoiceGenerationDto(
    string FormatId,
    string FormatName,
    bool Generated,
    IReadOnlyList<ComplianceIssueDto> Issues);

public sealed record ComplianceIssueDto(string? Field, string Message);

public sealed record EInvoiceFileDto(byte[] Content, string ContentType, string FileName);

/// <summary>A signing certificate as a PKCS#12 (.p12/.pfx) file, base64, with its password.</summary>
public sealed record UploadCertificateDto(string Pfx, string Password);
