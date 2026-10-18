using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Localization;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.Projects.Validators;

public sealed class UpdateProjectDtoValidator : AbstractValidator<UpdateProjectDto>
{
    public UpdateProjectDtoValidator(IAppText text)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithText(text, "validation.project.nameRequired")
            .MaximumLength(200).WithText(text, "validation.project.nameTooLong", ("max", 200));
    }
}
