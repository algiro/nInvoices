using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using nInvoices.Api.Infrastructure;
using Moq;
using nInvoices.Application;
using nInvoices.Application.Compliance;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Taxes.Commands;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
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
        // The handler's dependencies are strict mocks with no setup: if the handler ran, any call
        // on them would fail with a MockException instead of the validation failure
        var taxes = new Mock<IRepository<Tax>>(MockBehavior.Strict);
        var customers = new Mock<IRepository<Customer>>(MockBehavior.Strict);
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        unitOfWork.Setup(u => u.Dispose()); // by the scope
        var compliance = new Mock<ITaxCompliance>(MockBehavior.Strict);
        var services = new ServiceCollection()
            .AddLogging()
            .AddApplicationRequests()
            .AddScoped(_ => taxes.Object)
            .AddScoped(_ => customers.Object)
            .AddScoped(_ => unitOfWork.Object)
            .AddScoped(_ => compliance.Object);
        // Handlers are scoped (they use the request's DbContext): resolving them outside a scope must fail
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var command = new CreateTaxCommand(new CreateTaxDto(0, "", "", "PERCENTAGE", 21m, TaxApplicationType.OnSubtotal));

        var exception = await Should.ThrowAsync<ValidationException>(
            () => mediator.Send(command, TestContext.CurrentContext.CancellationToken).AsTask());

        exception.Errors.Select(e => e.PropertyName).ShouldBe(["Tax.CustomerId", "Tax.Description"], ignoreOrder: true);
        taxes.VerifyNoOtherCalls();
        unitOfWork.VerifyNoOtherCalls();
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
