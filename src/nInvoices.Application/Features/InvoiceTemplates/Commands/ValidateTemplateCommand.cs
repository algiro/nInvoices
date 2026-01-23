using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.InvoiceTemplates.Commands;

/// <summary>
/// Command to validate template syntax and extract placeholders.
/// Does not persist data - used for validation before saving.
/// </summary>
public sealed record ValidateTemplateCommand(string Content) : IRequest<TemplateValidationResultDto>;