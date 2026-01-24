using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Command to validate template content before saving.
/// </summary>
public sealed record ValidateTemplateCommand(string Content) : IRequest<TemplateValidationResultDto>;
