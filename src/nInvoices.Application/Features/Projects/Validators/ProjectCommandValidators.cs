using FluentValidation;
using nInvoices.Application.Features.Projects.Commands;

namespace nInvoices.Application.Features.Projects.Validators;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Project).NotNull().SetValidator(new CreateProjectDtoValidator());
    }
}

public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(x => x.Project).NotNull().SetValidator(new UpdateProjectDtoValidator());
    }
}
