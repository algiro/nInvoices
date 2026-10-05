using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.EmailTemplates.Commands;

/// <summary>
/// Updates an email template. The syntax of subject and body is checked before this runs
/// (<c>UpdateEmailTemplateCommandValidator</c>).
/// </summary>
public sealed class UpdateEmailTemplateCommandHandler : IRequestHandler<UpdateEmailTemplateCommand, EmailTemplateDto?>
{
    private readonly IRepository<EmailTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateEmailTemplateCommandHandler(IRepository<EmailTemplate> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<EmailTemplateDto?> Handle(UpdateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
            return null;

        template.Update(request.Template.Name, request.Template.Subject, request.Template.Body);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return EmailTemplateMapper.ToDto(template);
    }
}
