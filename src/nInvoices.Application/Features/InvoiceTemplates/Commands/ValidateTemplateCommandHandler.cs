using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Services;
using System.Text.RegularExpressions;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Handles template validation without persistence.
/// Useful for real-time validation in UI.
/// </summary>
public sealed class ValidateTemplateCommandHandler : IRequestHandler<ValidateTemplateCommand, TemplateValidationResultDto>
{
    private readonly ITemplateRenderer _templateRenderer;

    public ValidateTemplateCommandHandler(ITemplateRenderer templateRenderer)
    {
        _templateRenderer = templateRenderer;
    }

    public async Task<TemplateValidationResultDto> Handle(ValidateTemplateCommand request, CancellationToken cancellationToken)
    {
        // Validate syntax
        var validationResult = await _templateRenderer.ValidateAsync(request.Content, cancellationToken);
        
        // Extract placeholders using regex
        var placeholders = ExtractPlaceholders(request.Content);

        return new TemplateValidationResultDto(
            validationResult.IsValid,
            validationResult.Errors,
            placeholders);
    }

    private static List<string> ExtractPlaceholders(string content)
    {
        var regex = new Regex(@"\{\{\s*([a-zA-Z_][a-zA-Z0-9_\.]*)\s*\}\}", RegexOptions.Compiled);
        var matches = regex.Matches(content);
        
        return matches
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .OrderBy(p => p)
            .ToList();
    }
}