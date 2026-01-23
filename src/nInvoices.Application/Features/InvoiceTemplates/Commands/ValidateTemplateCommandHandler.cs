using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Handles template validation without persistence.
/// Useful for real-time validation in UI.
/// </summary>
public sealed class ValidateTemplateCommandHandler : IRequestHandler<ValidateTemplateCommand, TemplateValidationResultDto>
{
    private readonly ITemplateEngine _templateEngine;

    public ValidateTemplateCommandHandler(ITemplateEngine templateEngine)
    {
        _templateEngine = templateEngine;
    }

    public Task<TemplateValidationResultDto> Handle(ValidateTemplateCommand request, CancellationToken cancellationToken)
    {
        var isValid = _templateEngine.ValidateTemplate(request.Content, out var errors);
        var placeholders = _templateEngine.ExtractPlaceholders(request.Content);

        var result = new TemplateValidationResultDto(isValid, errors, placeholders);
        return Task.FromResult(result);
    }
}