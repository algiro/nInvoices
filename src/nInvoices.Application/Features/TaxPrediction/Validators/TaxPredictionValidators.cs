using FluentValidation;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.TaxPrediction.Commands;
using nInvoices.Application.Features.TaxPrediction.Queries;
using nInvoices.Application.Localization;
using nInvoices.Application.Validation;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Features.TaxPrediction.Validators;

public sealed class UpdateTaxProfileDtoValidator : AbstractValidator<UpdateTaxProfileDto>
{
    public UpdateTaxProfileDtoValidator(IAppText text)
    {
        RuleFor(x => x.CountryCode)
            .NotEmpty().WithText(text, "validation.taxes.chooseCountry")
            .Matches("^[A-Za-z]{2}$").WithText(text, "validation.countryIsoCode");

        RuleFor(x => x.RegimeId)
            .NotEmpty().WithText(text, "validation.taxes.chooseRegime")
            .MaximumLength(50).WithText(text, "validation.taxes.regimeTooLong", ("max", 50));
    }
}

public sealed class RecordTaxPaymentDtoValidator : AbstractValidator<RecordTaxPaymentDto>
{
    public RecordTaxPaymentDtoValidator(IAppText text)
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithText(text, "validation.yearRange");

        RuleFor(x => x.Period)
            .InclusiveBetween(1, TaxPayment.MaxPeriod).WithText(text, "validation.taxes.periodRange", ("max", TaxPayment.MaxPeriod));

        RuleFor(x => x.Form)
            .NotEmpty().WithText(text, "validation.taxes.formRequired")
            .MaximumLength(20).WithText(text, "validation.taxes.formTooLong", ("max", 20));

        // Zero is a real payment: a quarter with nothing to pay is still filed
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithText(text, "validation.amountNegative");
    }
}

public sealed class UpdateTaxProfileCommandValidator : AbstractValidator<UpdateTaxProfileCommand>
{
    public UpdateTaxProfileCommandValidator(IAppText text)
    {
        RuleFor(x => x.Profile).NotNull().SetValidator(new UpdateTaxProfileDtoValidator(text));
    }
}

public sealed class RecordTaxPaymentCommandValidator : AbstractValidator<RecordTaxPaymentCommand>
{
    public RecordTaxPaymentCommandValidator(IAppText text)
    {
        RuleFor(x => x.Payment).NotNull().SetValidator(new RecordTaxPaymentDtoValidator(text));
    }
}

public sealed class GetTaxPredictionQueryValidator : AbstractValidator<GetTaxPredictionQuery>
{
    public GetTaxPredictionQueryValidator(IAppText text)
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithText(text, "validation.yearRange");
    }
}
