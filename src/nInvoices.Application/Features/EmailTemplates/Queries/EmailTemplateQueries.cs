using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.EmailTemplates.Queries;

/// <summary>The email templates of a customer, or the shared ones when <paramref name="CustomerId"/> is null.</summary>
public sealed record GetEmailTemplatesQuery(long? CustomerId) : IRequest<IReadOnlyList<EmailTemplateDto>>;

/// <summary>An email template; null when it doesn't exist.</summary>
public sealed record GetEmailTemplateByIdQuery(long Id) : IRequest<EmailTemplateDto?>;

public sealed class EmailTemplateQueryHandlers :
    IRequestHandler<GetEmailTemplatesQuery, IReadOnlyList<EmailTemplateDto>>,
    IRequestHandler<GetEmailTemplateByIdQuery, EmailTemplateDto?>
{
    private readonly IRepository<EmailTemplate> _repository;

    public EmailTemplateQueryHandlers(IRepository<EmailTemplate> repository)
    {
        _repository = repository;
    }

    public async ValueTask<IReadOnlyList<EmailTemplateDto>> Handle(GetEmailTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await _repository.FindAsync(t => t.CustomerId == request.CustomerId, cancellationToken);
        return templates.Select(EmailTemplateMapper.ToDto).ToList();
    }

    public async ValueTask<EmailTemplateDto?> Handle(GetEmailTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return template is null ? null : EmailTemplateMapper.ToDto(template);
    }
}
