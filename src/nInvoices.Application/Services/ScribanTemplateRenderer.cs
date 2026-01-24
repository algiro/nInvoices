using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;

namespace nInvoices.Application.Services;

/// <summary>
/// Scriban-based template renderer.
/// Scriban is safe (no arbitrary code execution), fast (pre-compiled), and feature-rich.
/// </summary>
public sealed class ScribanTemplateRenderer : ITemplateRenderer
{
    private readonly ILogger<ScribanTemplateRenderer> _logger;
    private readonly ILocalizationService _localizationService;

    public ScribanTemplateRenderer(
        ILogger<ScribanTemplateRenderer> logger,
        ILocalizationService localizationService)
    {
        _logger = logger;
        _localizationService = localizationService;
    }

    public async Task<string> RenderAsync(
        string templateContent,
        object model,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateContent);
        ArgumentNullException.ThrowIfNull(model);

        try
        {
            // Convert user-friendly [[ ]] delimiters to Scriban's {{ }} syntax
            // This avoids conflicts with Vue.js template syntax in the frontend
            var scribanTemplate = templateContent
                .Replace("[[", "{{")
                .Replace("]]", "}}");

            var template = Template.Parse(scribanTemplate);

            if (template.HasErrors)
            {
                var errors = string.Join(", ", template.Messages.Select(m => m.Message));
                throw new InvalidOperationException($"Template syntax errors: {errors}");
            }

            var scriptObject = new ScriptObject();
            scriptObject.Import(model, renamer: member => ToCamelCase(member.Name));
            
            // Add custom functions
            scriptObject.Import(nameof(FormatCurrency), new Func<decimal, string, string>(FormatCurrency));
            scriptObject.Import(nameof(FormatDate), new Func<DateTime, string, string>(FormatDate));
            scriptObject.Import(nameof(FormatDecimal), new Func<decimal, int, string>(FormatDecimal));
            
            // Add localization functions
            scriptObject.Import(nameof(LocalizeDayOfWeek), new Func<DateTime, string, bool, string>(LocalizeDayOfWeek));
            scriptObject.Import(nameof(LocalizeMonth), new Func<int, string, bool, string>(LocalizeMonth));
            
            // Configure member accessor to use camelCase for all property access (including nested objects)
            var context = new TemplateContext
            {
                MemberRenamer = member => ToCamelCase(member.Name)
            };
            context.PushGlobal(scriptObject);

            var result = await template.RenderAsync(context);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render template");
            throw new InvalidOperationException("Template rendering failed. Check template syntax and model data.", ex);
        }
    }

    public Task<TemplateValidationResult> ValidateAsync(
        string templateContent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateContent))
        {
            return Task.FromResult(new TemplateValidationResult(
                false,
                new[] { "Template content cannot be empty" }));
        }

        try
        {
            // Convert [[ ]] to {{ }} for validation
            var scribanTemplate = templateContent
                .Replace("[[", "{{")
                .Replace("]]", "}}");

            var template = Template.Parse(scribanTemplate);

            if (template.HasErrors)
            {
                var errors = template.Messages
                    .Select(m => $"Line {m.Span.Start.Line + 1}, Column {m.Span.Start.Column + 1}: {m.Message}")
                    .ToList();
                return Task.FromResult(new TemplateValidationResult(false, errors));
            }

            return Task.FromResult(new TemplateValidationResult(true, Array.Empty<string>()));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Template validation failed with exception");
            return Task.FromResult(new TemplateValidationResult(
                false,
                new[] { $"Validation error: {ex.Message}" }));
        }
    }

    // Custom functions available in templates

    /// <summary>
    /// Formats a decimal as currency: FormatCurrency(100.50, "EUR") => "100.50 EUR"
    /// </summary>
    private static string FormatCurrency(decimal amount, string currency)
    {
        return $"{amount:N2} {currency}";
    }

    /// <summary>
    /// Formats a date: FormatDate(date, "yyyy-MM-dd") => "2024-01-23"
    /// </summary>
    private static string FormatDate(DateTime date, string format)
    {
        return date.ToString(format);
    }

    /// <summary>
    /// Formats a decimal with specific precision: FormatDecimal(123.456, 2) => "123.46"
    /// </summary>
    private static string FormatDecimal(decimal value, int decimals)
    {
        return Math.Round(value, decimals).ToString($"F{decimals}");
    }

    /// <summary>
    /// Localizes day of week name: LocalizeDayOfWeek(date, "it-IT") => "Lunedì"
    /// </summary>
    private string LocalizeDayOfWeek(DateTime date, string locale, bool shortFormat = false)
    {
        return _localizationService.GetDayOfWeek(date.DayOfWeek, locale, shortFormat);
    }

    /// <summary>
    /// Localizes month name: LocalizeMonth(1, "it-IT") => "Gennaio"
    /// </summary>
    private string LocalizeMonth(int monthNumber, string locale, bool shortFormat = false)
    {
        return _localizationService.GetMonthName(monthNumber, locale, shortFormat);
    }

    /// <summary>
    /// Converts PascalCase to camelCase for consistent template syntax.
    /// C# properties are PascalCase, but templates use camelCase by convention.
    /// </summary>
    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            return name;

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
