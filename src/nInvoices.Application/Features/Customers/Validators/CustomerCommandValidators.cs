using FluentValidation;
using nInvoices.Application.Features.Customers.Commands;
using nInvoices.Application.Localization;

namespace nInvoices.Application.Features.Customers.Validators;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator(IAppText text)
    {
        RuleFor(x => x.Customer).NotNull().SetValidator(new CreateCustomerDtoValidator(text));
    }
}

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator(IAppText text)
    {
        RuleFor(x => x.Customer).NotNull().SetValidator(new UpdateCustomerDtoValidator(text));
    }
}
