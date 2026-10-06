using nInvoices.Core.Compliance;

namespace nInvoices.Core.Exceptions;

/// <summary>
/// A business rule refused the operation, e.g. paying a draft or generating an invoice without a
/// rate. The message is written for the user and is returned as is (400). Derives from
/// <see cref="InvalidOperationException"/>, so code catching that still catches it.
/// </summary>
public class DomainException : InvalidOperationException
{
    public DomainException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <summary>Machine-readable reason the UI can react to (e.g. <c>gmail_not_connected</c>); null when there is none.</summary>
    public string? Code { get; init; }

    /// <summary>The fields that break the rule, when the refusal is about data the user can fix.</summary>
    public IReadOnlyList<ComplianceIssue> Issues { get; init; } = [];
}
