using nInvoices.Core.Compliance;

namespace nInvoices.Application.Features.Compliance.Commands;

/// <summary>The settings do not meet a country rules; <see cref="Issues"/> says which fields.</summary>
public sealed class ComplianceValidationException : ArgumentException
{
    public IReadOnlyList<ComplianceIssue> Issues { get; }

    public ComplianceValidationException(IReadOnlyList<ComplianceIssue> issues)
        : base(string.Join("; ", issues.Select(i => i.Message)))
    {
        Issues = issues;
    }
}
