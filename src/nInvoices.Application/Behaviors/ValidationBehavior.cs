using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace nInvoices.Application.Behaviors;

/// <summary>
/// Runs every validator registered for the request before its handler, and throws a
/// <see cref="ValidationException"/> with all the failures when any rule fails, so the handler
/// never sees invalid input. Requests without a validator pass straight through.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IReadOnlyList<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators.ToList();
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Count == 0)
            return await next(cancellationToken);

        var failures = new List<ValidationFailure>();
        foreach (var validator in _validators)
        {
            // A context collects the failures of every validation it is used for: one per validator
            var result = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next(cancellationToken);
    }
}
