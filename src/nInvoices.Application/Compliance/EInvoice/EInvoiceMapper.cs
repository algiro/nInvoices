using nInvoices.Application.DTOs;
using nInvoices.Core.Compliance;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Compliance.EInvoice;

public static class EInvoiceMapper
{
    public static InvoiceEInvoiceDto ToDto(EInvoiceStatus status, string invoiceNumber) => new(
        status.Module.CountryCode,
        status.Module.DisplayName,
        status.Format.FormatId,
        status.Format.DisplayName,
        status.IsMandatory,
        status.Stored is not null,
        status.Stored?.GeneratedAt,
        status.Stored is null ? null : FileName(invoiceNumber, status.Stored),
        status.Stored?.Sha256);

    public static EInvoiceGenerationDto ToDto(EInvoiceResult result) => new(
        result.Format.FormatId,
        result.Format.DisplayName,
        result.Generated,
        result.Issues.Select(ToDto).ToList());

    public static ComplianceIssueDto ToDto(ComplianceIssue issue) => new(issue.Field, issue.Message);

    /// <summary>"26-10-001.xsig": the invoice number, made safe for a file name.</summary>
    public static string FileName(string invoiceNumber, InvoiceEInvoice stored)
    {
        var safe = new string(invoiceNumber.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_').ToArray());
        return $"{safe}.{stored.FileExtension}";
    }
}
