using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Exceptions;

namespace nInvoices.Api.Infrastructure;

/// <summary>
/// The one place that turns an exception escaping a controller into a response, so actions don't
/// catch to translate. Every response is <see cref="ProblemDetails"/> (<c>application/problem+json</c>)
/// that also carries <c>error</c>, the message the UI shows, plus <c>code</c> / <c>issues</c> when the
/// exception has them:
/// <list type="bullet">
/// <item><see cref="ValidationException"/> (the validation pipeline) → 400, with <c>errors</c> per field</item>
/// <item><see cref="NotFoundException"/> → 404</item>
/// <item><see cref="InvoiceEmailException"/> about the Gmail connection → 409, others → 400</item>
/// <item><see cref="DomainException"/> (a business rule refused) → 400</item>
/// <item><see cref="ArgumentException"/> (an entity rejecting its input) → 400; not <see cref="ArgumentNullException"/>, a bug</item>
/// <item>anything else → 500, logged, with a generic message (the exception only in Development)</item>
/// </list>
/// A cancelled request is left alone.
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    private const string UnexpectedMessage = "Something went wrong on the server. The error was logged; please try again.";

    private readonly ILogger<ApiExceptionFilter> _logger;
    private readonly IHostEnvironment _environment;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public void OnException(ExceptionContext context)
    {
        var exception = context.Exception;
        if (exception is OperationCanceledException && context.HttpContext.RequestAborted.IsCancellationRequested)
            return;

        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = exception switch
        {
            ValidationException validation => ValidationProblem(context.HttpContext, factory, validation),
            NotFoundException => Problem(StatusCodes.Status404NotFound, exception.Message),
            InvoiceEmailException { IsGmailSetupProblem: true } => Problem(StatusCodes.Status409Conflict, exception.Message),
            DomainException or ArgumentException and not ArgumentNullException => Problem(StatusCodes.Status400BadRequest, exception.Message),
            _ => null
        };

        if (problem is null)
        {
            _logger.LogError(exception, "Unhandled exception in {Method} {Path}", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            problem = Problem(StatusCodes.Status500InternalServerError, _environment.IsDevelopment() ? exception.ToString() : null, UnexpectedMessage);
        }
        else
        {
            _logger.LogInformation("{Method} {Path} refused ({Status}): {Message}",
                context.HttpContext.Request.Method, context.HttpContext.Request.Path, problem.Status, problem.Extensions["error"]);
        }

        if (exception is DomainException domain)
        {
            if (domain.Code is not null)
                problem.Extensions["code"] = domain.Code;
            if (domain.Issues.Count > 0)
                problem.Extensions["issues"] = domain.Issues.Select(i => new { field = i.Field, message = i.Message }).ToList();
        }

        context.Result = new ObjectResult(problem) { StatusCode = problem.Status, ContentTypes = { "application/problem+json" } };
        context.ExceptionHandled = true;

        ProblemDetails Problem(int status, string? detail, string? error = null)
        {
            var details = factory.CreateProblemDetails(context.HttpContext, status, detail: detail);
            details.Extensions["error"] = error ?? detail;
            return details;
        }
    }

    private static ValidationProblemDetails ValidationProblem(HttpContext httpContext, ProblemDetailsFactory factory, ValidationException exception)
    {
        var modelState = new ModelStateDictionary();
        foreach (var failure in exception.Errors)
            modelState.AddModelError(ToJsonPath(failure.PropertyName), failure.ErrorMessage);

        var problem = factory.CreateValidationProblemDetails(httpContext, modelState, StatusCodes.Status400BadRequest);
        // In the order the rules failed (the per-field dictionary doesn't keep it)
        problem.Extensions["error"] = Sentences(exception.Errors.Select(f => f.ErrorMessage));
        return problem;
    }

    /// <summary>
    /// The sentence for <c>error</c> of a ProblemDetails the filter didn't write: the field messages of
    /// a validation problem (e.g. a body that doesn't bind), otherwise its detail or title.
    /// </summary>
    public static string? MessageOf(ProblemDetails problem) =>
        problem is HttpValidationProblemDetails { Errors.Count: > 0 } validation
            ? Sentences(validation.Errors.SelectMany(e => e.Value))
            : problem.Detail ?? problem.Title;

    private static string Sentences(IEnumerable<string> messages) =>
        string.Join(" ", messages.Select(m => m.TrimEnd('.') + ".").Distinct());

    // "Customer.Address.City" / "Invoice.WorkDays[0].Date" → the camelCase names of the JSON body
    private static string ToJsonPath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
