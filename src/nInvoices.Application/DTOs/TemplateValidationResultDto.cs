namespace nInvoices.Application.DTOs;

/// <summary>
/// Result of template validation.
/// </summary>
public sealed record TemplateValidationResultDto(
    bool IsValid,
    IEnumerable<string> Errors,
    IEnumerable<string> Placeholders);