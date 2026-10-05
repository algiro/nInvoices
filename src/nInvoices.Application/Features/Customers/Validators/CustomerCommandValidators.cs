using FluentValidation;
using nInvoices.Application.Features.Customers.Commands;

namespace nInvoices.Application.Features.Customers.Validators;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Customer).NotNull().SetValidator(new CreateCustomerDtoValidator());
    }
}

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Customer).NotNull().SetValidator(new UpdateCustomerDtoValidator());
    }
}
