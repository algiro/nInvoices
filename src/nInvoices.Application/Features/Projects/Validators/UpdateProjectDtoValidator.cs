using FluentValidation;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Projects.Validators;

public sealed class UpdateProjectDtoValidator : AbstractValidator<UpdateProjectDto>
{
    public UpdateProjectDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Project name is required")
            .MaximumLength(200).WithMessage("Project name must not exceed 200 characters");
    }
}
