using System.Text;
using System.Text.RegularExpressions;
using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.TemplateEngine;

/// <summary>
/// Handlebars-style template engine implementation.
/// Supports placeholders: {{Variable}}, {{Object.Property}}, {{Variable.Lang}}
/// </summary>
public sealed class HandlebarsTemplateEngine : ITemplateEngine
{
    private static readonly Regex PlaceholderRegex = new(
        @"\{\{([^}]+)\}\}",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public string Render(string template, IDictionary<string, object> variables)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(variables);

        var result = template;
        var matches = PlaceholderRegex.Matches(template);

        foreach (Match match in matches)
        {
            var placeholder = match.Groups[1].Value.Trim();
            var value = ResolvePlaceholder(placeholder, variables);
            result = result.Replace(match.Value, value);
        }

        return result;
    }

    public IEnumerable<string> ExtractPlaceholders(string template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var matches = PlaceholderRegex.Matches(template);
        var placeholders = new HashSet<string>();

        foreach (Match match in matches)
        {
            placeholders.Add(match.Groups[1].Value.Trim());
        }

        return placeholders;
    }

    public bool ValidateTemplate(string template, out IEnumerable<string> errors)
    {
        ArgumentNullException.ThrowIfNull(template);

        var errorList = new List<string>();

        // Check for empty placeholders
        if (template.Contains("{{}}"))
        {
            errorList.Add("Empty placeholder found: {{}}");
        }

        // Check for nested placeholders
        var nestedPlaceholders = Regex.Matches(template, @"\{\{[^}]*\{\{");
        if (nestedPlaceholders.Count > 0)
        {
            errorList.Add("Nested placeholders are not allowed");
        }

        // Count double braces for proper matching
        var openDoubleBraces = Regex.Matches(template, @"\{\{").Count;
        var closeDoubleBraces = Regex.Matches(template, @"\}\}").Count;

        if (openDoubleBraces != closeDoubleBraces)
        {
            errorList.Add($"Unmatched placeholder braces: {openDoubleBraces} opening, {closeDoubleBraces} closing");
        }

        errors = errorList;
        return errorList.Count == 0;
    }

    private static string ResolvePlaceholder(string placeholder, IDictionary<string, object> variables)
    {
        // Handle nested properties (e.g., Customer.Name)
        if (placeholder.Contains('.'))
        {
            return ResolveNestedProperty(placeholder, variables);
        }

        // Direct variable lookup
        if (variables.TryGetValue(placeholder, out var value))
        {
            return FormatValue(value);
        }

        // Return placeholder unchanged if not found (don't throw, allow partial rendering)
        return $"{{{{{placeholder}}}}}";
    }

    private static string ResolveNestedProperty(string placeholder, IDictionary<string, object> variables)
    {
        var parts = placeholder.Split('.');
        
        if (parts.Length < 2)
            return $"{{{{{placeholder}}}}}";

        // Try to get the root object
        if (!variables.TryGetValue(parts[0], out var current))
            return $"{{{{{placeholder}}}}}";

        // Navigate through the property path
        for (int i = 1; i < parts.Length; i++)
        {
            if (current is null)
                return string.Empty;

            // Handle dictionaries (for localization support)
            if (current is IDictionary<string, object> dict)
            {
                if (dict.TryGetValue(parts[i], out var dictValue))
                {
                    current = dictValue;
                    continue;
                }
                return $"{{{{{placeholder}}}}}";
            }

            // Handle regular objects via reflection
            var property = current.GetType().GetProperty(parts[i]);
            if (property is null)
                return $"{{{{{placeholder}}}}}";

            current = property.GetValue(current);
        }

        return FormatValue(current);
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s,
            DateTime dt => dt.ToString("yyyy-MM-dd"),
            DateOnly d => d.ToString("yyyy-MM-dd"),
            decimal dec => dec.ToString("N2"),
            double dbl => dbl.ToString("N2"),
            float flt => flt.ToString("N2"),
            _ => value.ToString() ?? string.Empty
        };
    }
}
