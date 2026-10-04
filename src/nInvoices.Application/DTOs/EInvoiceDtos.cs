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

/// <summary>Where the e-invoice of an invoice was delivered and where it stands there.</summary>
public sealed record EInvoiceSubmissionDto(
    string Reference,
    string Environment,
    DateTime SubmittedAt,
    DateTime? RegisteredAt,
    string? StatusCode,
    string? StatusName,
    string? CancellationStatus,
    DateTime? CheckedAt,
    string? LastError);

/// <summary>A delivery channel (FACe...) for an invoice: whether it applies, what is missing, and the delivery if made.</summary>
/// <param name="CanSend">Everything needed to send now is in place.</param>
/// <param name="Problems">What stands in the way of sending (empty when <see cref="CanSend"/>).</param>
public sealed record EInvoiceChannelDto(
    string ChannelId,
    string DisplayName,
    string CountryCode,
    string FormatId,
    string Environment,
    bool CanSend,
    IReadOnlyList<string> Problems,
    EInvoiceSubmissionDto? Submission);

/// <summary>The outcome of sending or refreshing; <see cref="Message"/> says why when it failed.</summary>
public sealed record EInvoiceDeliveryDto(bool Succeeded, string? Message, EInvoiceSubmissionDto? Submission);
