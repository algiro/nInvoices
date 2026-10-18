using FluentValidation;
using nInvoices.Application.Features.Projects.Commands;
using nInvoices.Application.Localization;

namespace nInvoices.Application.Features.Projects.Validators;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator(IAppText text)
    {
        RuleFor(x => x.Project).NotNull().SetValidator(new CreateProjectDtoValidator(text));
    }
}

public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator(IAppText text)
    {
        RuleFor(x => x.Project).NotNull().SetValidator(new UpdateProjectDtoValidator(text));
    }
}
