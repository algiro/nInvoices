using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace nInvoices.Api.Infrastructure;

/// <summary>
/// Turns the <see cref="ValidationException"/> thrown by the Mediator validation pipeline into a
/// 400 <see cref="ValidationProblemDetails"/>: <c>errors</c> lists the messages per field
/// (e.g. <c>customer.address.city</c>), and <c>error</c> joins them in one sentence, the shape
/// every other 400 of the API has, so clients that only read <c>error</c> show the problem.
/// </summary>
public sealed class ValidationExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ValidationException exception)
            return;

        var modelState = new ModelStateDictionary();
        foreach (var failure in exception.Errors)
            modelState.AddModelError(ToJsonPath(failure.PropertyName), failure.ErrorMessage);

        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(context.HttpContext, modelState, StatusCodes.Status400BadRequest);
        problem.Extensions["error"] = string.Join(" ", exception.Errors
            .Select(f => f.ErrorMessage.TrimEnd('.') + ".")
            .Distinct());

        context.Result = new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        context.ExceptionHandled = true;
    }

    // "Customer.Address.City" / "Invoice.WorkDays[0].Date" → the camelCase names of the JSON body
    private static string ToJsonPath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
