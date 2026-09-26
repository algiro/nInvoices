namespace nInvoices.Core.Interfaces;

/// <summary>
/// Defines template rendering functionality.
/// Supports Handlebars-style placeholders: {{Variable}}
/// </summary>
public interface ITemplateEngine
{
    /// <summary>
    /// Renders a template with the provided variables.
    /// </summary>
    string Render(string template, IDictionary<string, object> variables);

    /// <summary>
    /// Extracts all placeholder names from a template.
    /// </summary>
    IEnumerable<string> ExtractPlaceholders(string template);

    /// <summary>
    /// Validates a template for syntax errors.
    /// </summary>
    bool ValidateTemplate(string template, out IEnumerable<string> errors);
}
