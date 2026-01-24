namespace nInvoices.Application.Services;

/// <summary>
/// Renders template strings with provided model data.
/// Supports Scriban/Liquid syntax for loops, conditionals, and filters.
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// Renders a template with the provided model data.
    /// </summary>
    /// <param name="templateContent">The template content with placeholders (e.g., {{ customer.name }})</param>
    /// <param name="model">The data model to render into the template</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rendered template as string (typically HTML)</returns>
    Task<string> RenderAsync(string templateContent, object model, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates template syntax without rendering.
    /// Use this to check templates before saving them.
    /// </summary>
    /// <param name="templateContent">The template content to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with errors if any</returns>
    Task<TemplateValidationResult> ValidateAsync(string templateContent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of template validation.
/// </summary>
/// <param name="IsValid">True if template syntax is valid</param>
/// <param name="Errors">List of validation error messages</param>
public sealed record TemplateValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors
);
