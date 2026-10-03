namespace nInvoices.Application.DTOs;

/// <summary>Where an invoice stands in the user Verifactu chain.</summary>
/// <param name="IsRecorded">The invoice has a record (it was issued with Verifactu on).</param>
/// <param name="QrUrl">The URL the QR code of the invoice holds.</param>
/// <param name="QrSvg">The QR code as an SVG image.</param>
public sealed record InvoiceVerifactuDto(
    bool IsRecorded,
    long? Sequence,
    string? Hash,
    string? GeneratedAt,
    string? QrUrl,
    string? QrSvg,
    string Legend,
    string QrHeading,
    bool IsCancelled,
    string? SubmissionStatus = null,
    string? SubmissionMessage = null,
    string? Csv = null);

public sealed record ChainProblemDto(long Sequence, string Message);

public sealed record ChainReportDto(int Records, bool IsIntact, IReadOnlyList<ChainProblemDto> Problems);

/// <summary>Where the records stand with the Tax Agency.</summary>
public sealed record VerifactuStatusDto(int Pending, int Accepted, int AcceptedWithErrors, int Rejected, string? FirstProblem);

/// <summary>What one round of sending came to.</summary>
public sealed record VerifactuSubmissionRunDto(int Sent, int Accepted, int AcceptedWithErrors, int Rejected, string? Problem);
