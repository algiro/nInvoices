using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using nInvoices.Api.Infrastructure;
using nInvoices.Application;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Taxes.Commands;
using nInvoices.Core.Enums;
using Shouldly;

namespace nInvoices.Api.Tests.Infrastructure;

[TestFixture]
public sealed class ValidationExceptionFilterTests
{
    [Test]
    public void OnException_WithValidationException_Returns400ProblemWithFieldsAndMessage()
    {
        var context = CreateContext(new ValidationException(
        [
            new ValidationFailure("Customer.Name", "Customer name is required"),
            new ValidationFailure("Customer.Address.ZipCode", "Zip code is required.")
        ]));

        new ValidationExceptionFilter().OnException(context);

        context.ExceptionHandled.ShouldBeTrue();
        var result = context.Result.ShouldBeOfType<BadRequestObjectResult>();
        var problem = result.Value.ShouldBeOfType<ValidationProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Errors.Keys.ShouldBe(["customer.name", "customer.address.zipCode"], ignoreOrder: true);
        problem.Errors["customer.name"].ShouldBe(["Customer name is required"]);
        // The frontend shows "error", like for every other 400 of the API
        problem.Extensions["error"].ShouldBe("Customer name is required. Zip code is required.");
    }

    [Test]
    public void OnException_WithOtherException_LeavesItUnhandled()
    {
        var context = CreateContext(new InvalidOperationException("boom"));

        new ValidationExceptionFilter().OnException(context);

        context.ExceptionHandled.ShouldBeFalse();
        context.Result.ShouldBeNull();
    }

    [Test]
    public async Task ApplicationRequests_InvalidCommand_IsRejectedBeforeItsHandler()
    {
        // Only the request pipeline is registered: the handler's dependencies are missing, so
        // reaching the handler would fail with a different exception
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddApplicationRequests()
            .BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var command = new CreateTaxCommand(new CreateTaxDto(0, "", "", "PERCENTAGE", 21m, TaxApplicationType.OnSubtotal));

        var exception = await Should.ThrowAsync<ValidationException>(
            () => mediator.Send(command, TestContext.CurrentContext.CancellationToken));

        exception.Errors.Select(e => e.PropertyName).ShouldBe(["Tax.CustomerId", "Tax.Description"], ignoreOrder: true);
    }

    private static ExceptionContext CreateContext(Exception exception)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddControllers();
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ExceptionContext(actionContext, []) { Exception = exception };
    }
}
