using Mediator;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.EmailTemplates.Commands;

/// <summary>Changes an email template's name, subject and body; null when it doesn't exist.</summary>
public sealed record UpdateEmailTemplateCommand(long Id, UpdateEmailTemplateDto Template) : IRequest<EmailTemplateDto?>;
