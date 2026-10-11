using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.TaxPrediction.Commands;
using nInvoices.Application.Features.TaxPrediction.Queries;

namespace nInvoices.Application.Features.TaxPrediction.Validators;

public sealed class UpdateTaxProfileDtoValidator : AbstractValidator<UpdateTaxProfileDto>
{
    public UpdateTaxProfileDtoValidator()
    {
        RuleFor(x => x.CountryCode)
            .NotEmpty().WithMessage("Choose the country where you pay taxes")
            .Matches("^[A-Za-z]{2}$").WithMessage("The country must be a two-letter ISO code");

        RuleFor(x => x.RegimeId)
            .NotEmpty().WithMessage("Choose a tax regime")
            .MaximumLength(50).WithMessage("The tax regime must not exceed 50 characters");
    }
}

public sealed class RecordTaxPaymentDtoValidator : AbstractValidator<RecordTaxPaymentDto>
{
    public RecordTaxPaymentDtoValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year must be between 2000 and 2100");

        RuleFor(x => x.Period)
            .InclusiveBetween(1, 12).WithMessage("Period must be between 1 and 12");

        RuleFor(x => x.Form)
            .NotEmpty().WithMessage("The tax form is required")
            .MaximumLength(20).WithMessage("The tax form must not exceed 20 characters");

        // Zero is a real payment: a quarter with nothing to pay is still filed
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("The amount cannot be negative");
    }
}

public sealed class UpdateTaxProfileCommandValidator : AbstractValidator<UpdateTaxProfileCommand>
{
    public UpdateTaxProfileCommandValidator()
    {
        RuleFor(x => x.Profile).NotNull().SetValidator(new UpdateTaxProfileDtoValidator());
    }
}

public sealed class RecordTaxPaymentCommandValidator : AbstractValidator<RecordTaxPaymentCommand>
{
    public RecordTaxPaymentCommandValidator()
    {
        RuleFor(x => x.Payment).NotNull().SetValidator(new RecordTaxPaymentDtoValidator());
    }
}

public sealed class GetTaxPredictionQueryValidator : AbstractValidator<GetTaxPredictionQuery>
{
    public GetTaxPredictionQueryValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year must be between 2000 and 2100");
    }
}
