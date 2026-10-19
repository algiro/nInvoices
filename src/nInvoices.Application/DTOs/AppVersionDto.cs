namespace nInvoices.Application.DTOs;

/// <summary>The build of the running API: its release number and the commit it was built from (null when unknown).</summary>
public sealed record AppVersionDto(string Version, string? Commit);
