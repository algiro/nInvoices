using FluentValidation;
using Mediator;
using nInvoices.Application.Behaviors;
using Shouldly;

namespace nInvoices.Application.Tests.Behaviors;

[TestFixture]
public sealed class ValidationBehaviorTests
{
    private sealed record SampleRequest(string Name, int Count) : IRequest<string>;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private static InlineValidator<SampleRequest> NameRequired()
    {
        var validator = new InlineValidator<SampleRequest>();
        validator.RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
        return validator;
    }

    private static InlineValidator<SampleRequest> CountPositive()
    {
        var validator = new InlineValidator<SampleRequest>();
        validator.RuleFor(x => x.Count).GreaterThan(0).WithMessage("Count must be positive");
        return validator;
    }

    [Test]
    public async Task Handle_WithoutValidators_CallsHandler()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([]);

        var result = await behavior.Handle(new SampleRequest("", 0), (_, _) => ValueTask.FromResult("handled"), Token);

        result.ShouldBe("handled");
    }

    [Test]
    public async Task Handle_WhenValid_CallsHandler()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([NameRequired(), CountPositive()]);

        var result = await behavior.Handle(new SampleRequest("ok", 1), (_, _) => ValueTask.FromResult("handled"), Token);

        result.ShouldBe("handled");
    }

    [Test]
    public async Task Handle_WhenInvalid_ThrowsWithFailuresOfAllValidatorsAndSkipsHandler()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([NameRequired(), CountPositive()]);
        var handlerCalled = false;

        var exception = await Should.ThrowAsync<ValidationException>(() => behavior.Handle(
            new SampleRequest("", 0),
            (_, _) =>
            {
                handlerCalled = true;
                return ValueTask.FromResult("handled");
            },
            Token).AsTask());

        handlerCalled.ShouldBeFalse();
        exception.Errors.Select(e => e.ErrorMessage).ShouldBe(["Name is required", "Count must be positive"], ignoreOrder: true);
    }
}
