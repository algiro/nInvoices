using Mediator;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.EmailTemplates.Commands;

/// <summary>Creates an invoice email template, for a customer or shared (no customer).</summary>
public sealed record CreateEmailTemplateCommand(CreateEmailTemplateDto Template) : IRequest<EmailTemplateDto>;
