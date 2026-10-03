namespace nInvoices.Core.Compliance;

/// <summary>
/// The country-neutral concepts a tax-compliance regime can ask of an invoicing tool. A country
/// module declares only the ones its rules involve, so a country that needs just the issuer's
/// identity does not carry the machinery of one that needs signed XML and tamper-evident records.
/// Each later feature (e-invoice formats, signing, submission...) keys off one of these.
/// </summary>
public enum ComplianceCapability
{
    /// <summary>The issuer's legal name, tax id and address are required, and validated by country rules.</summary>
    IssuerIdentity,

    /// <summary>Customers need extra fiscal data (tax id type, public-body routing codes...).</summary>
    CustomerFiscalIdentity,

    /// <summary>Invoices must be issued in a structured, country-specified format (XML...).</summary>
    StructuredEInvoice,

    /// <summary>Invoices must carry a qualified electronic signature.</summary>
    ElectronicSignature,

    /// <summary>Invoices are submitted to, or routed through, a government platform.</summary>
    AuthoritySubmission,

    /// <summary>Invoice records must be chained and unalterable once issued.</summary>
    TamperEvidentRecords,

    /// <summary>Invoices must print a QR code or similar verification mark.</summary>
    VerificationMark
}
