namespace nInvoices.Core.Compliance;

/// <summary>A problem found when checking the user's compliance settings.</summary>
/// <param name="Field">The field it concerns (<c>legalName</c>, <c>taxId</c>, <c>address</c> or a module field key), or null if general.</param>
public sealed record ComplianceIssue(string? Field, string Message);
