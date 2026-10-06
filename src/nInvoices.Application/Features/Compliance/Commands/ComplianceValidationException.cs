using nInvoices.Core.Compliance;
using nInvoices.Core.Exceptions;

namespace nInvoices.Application.Features.Compliance.Commands;

/// <summary>The settings do not meet a country rules; <see cref="Issues"/> says which fields.</summary>
public sealed class ComplianceValidationException : DomainException
{
    public ComplianceValidationException(IReadOnlyList<ComplianceIssue> issues)
        : base(string.Join("; ", issues.Select(i => i.Message)))
    {
        Issues = issues;
    }
}
