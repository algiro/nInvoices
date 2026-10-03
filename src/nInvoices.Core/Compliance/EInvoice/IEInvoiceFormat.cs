using System.Security.Cryptography.X509Certificates;

namespace nInvoices.Core.Compliance.EInvoice;

/// <summary>The file a format produced for one invoice.</summary>
public sealed record EInvoiceArtifact(byte[] Content, string ContentType, string FileExtension);

/// <summary>
/// One structured e-invoice format of one country (Facturae, UBL...). A country can have several;
/// each declares whether it needs a signature, validates the document against its own rules and
/// builds the file.
/// </summary>
public interface IEInvoiceFormat
{
    /// <summary>Stable identifier, stored with the generated file, e.g. "facturae-3.2.2".</summary>
    string FormatId { get; }

    /// <summary>The country (ISO alpha-2) whose module this format belongs to.</summary>
    string CountryCode { get; }

    string DisplayName { get; }

    /// <summary>
    /// Whether the rules require this format for invoices to this customer (so it is generated when
    /// the invoice is finalized, rather than only on request).
    /// </summary>
    bool IsMandatoryFor(EInvoiceParty buyer);

    /// <summary>Whether <see cref="Build"/> needs a signing certificate.</summary>
    bool RequiresSignature { get; }

    /// <summary>Everything that stops this document from being a valid invoice in this format; empty if none.</summary>
    IReadOnlyList<ComplianceIssue> Validate(EInvoiceDocument document);

    /// <param name="signingCertificate">With a private key; required when <see cref="RequiresSignature"/>.</param>
    /// <exception cref="InvalidOperationException">The document is not valid or the certificate is missing.</exception>
    EInvoiceArtifact Build(EInvoiceDocument document, X509Certificate2? signingCertificate);
}
