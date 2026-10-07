using System.Globalization;
using Mediator;
using Microsoft.Extensions.Logging;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.EmailTemplates.Commands;

/// <summary>
/// Creates an email template. The first template of a customer, or the first shared one, becomes
/// active. The syntax of subject and body is checked before this runs
/// (<c>CreateEmailTemplateCommandValidator</c>); the entity rejects blank parts.
/// </summary>
public sealed class CreateEmailTemplateCommandHandler : IRequestHandler<CreateEmailTemplateCommand, EmailTemplateDto>
{
    private readonly IRepository<EmailTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateEmailTemplateCommandHandler> _logger;

    public CreateEmailTemplateCommandHandler(
        IRepository<EmailTemplate> repository,
        IUnitOfWork unitOfWork,
        ILogger<CreateEmailTemplateCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async ValueTask<EmailTemplateDto> Handle(CreateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Template;
        var template = new EmailTemplate(dto.CustomerId, dto.Name, dto.Subject, dto.Body);
        var existing = await _repository.FindAsync(t => t.CustomerId == dto.CustomerId, cancellationToken);
        if (!existing.Any())
            template.Activate();

        await _repository.AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Email template {TemplateId} created for customer {CustomerId}",
            template.Id,
            template.CustomerId?.ToString(CultureInfo.InvariantCulture) ?? "(shared)");

        return EmailTemplateMapper.ToDto(template);
    }
}
