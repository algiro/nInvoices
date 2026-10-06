using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using nInvoices.Api.Infrastructure;
using nInvoices.Application;
using nInvoices.Application.Compliance;
using nInvoices.Application.DTOs;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Features.Compliance.Commands;
using nInvoices.Application.Features.Taxes.Commands;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Compliance;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Exceptions;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Api.Tests.Infrastructure;

[TestFixture]
public sealed class ApiExceptionFilterTests
{
    [Test]
    public void ValidationException_Is400WithTheFieldsAndOneMessage()
    {
        var problem = Handle(new ValidationException(
        [
            new ValidationFailure("Customer.Name", "Customer name is required"),
            new ValidationFailure("Customer.Address.ZipCode", "Zip code is required.")
        ])).ShouldBeOfType<ValidationProblemDetails>();

        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Errors.Keys.ShouldBe(["customer.name", "customer.address.zipCode"], ignoreOrder: true);
        problem.Errors["customer.name"].ShouldBe(["Customer name is required"]);
        // The frontend shows "error", like for every other refusal of the API
        problem.Extensions["error"].ShouldBe("Customer name is required. Zip code is required.");
    }

    [Test]
    public void NotFoundException_Is404()
    {
        var problem = Handle(new NotFoundException("Invoice 42 not found"));

        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.Detail.ShouldBe("Invoice 42 not found");
        problem.Extensions["error"].ShouldBe("Invoice 42 not found");
    }

    [Test]
    public void DomainException_Is400WithItsMessage()
    {
        var problem = Handle(new DomainException("Invoice 26-10-001 cannot be marked as paid: not finalized yet."));

        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Extensions["error"].ShouldBe("Invoice 26-10-001 cannot be marked as paid: not finalized yet.");
        problem.Extensions.ShouldNotContainKey("code");
        problem.Extensions.ShouldNotContainKey("issues");
    }

    [Test]
    public void ComplianceValidationException_CarriesTheIssuesTheFormShowsPerField()
    {
        var problem = Handle(new ComplianceValidationException([new ComplianceIssue("dir3OficinaContable", "Required for public administrations")]));

        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        var issue = problem.Extensions["issues"].ShouldBeAssignableTo<System.Collections.IEnumerable>()!.Cast<object>().ShouldHaveSingleItem();
        issue.GetType().GetProperty("field")!.GetValue(issue).ShouldBe("dir3OficinaContable");
        issue.GetType().GetProperty("message")!.GetValue(issue).ShouldBe("Required for public administrations");
    }

    [TestCase(InvoiceEmailException.GmailNotConnected, StatusCodes.Status409Conflict)]
    [TestCase(InvoiceEmailException.GmailNotConfigured, StatusCodes.Status409Conflict)]
    [TestCase(InvoiceEmailException.GmailReconnectRequired, StatusCodes.Status409Conflict)]
    [TestCase(InvoiceEmailException.InvalidRecipients, StatusCodes.Status400BadRequest)]
    public void InvoiceEmailException_IsConflictForGmailSetup_WithItsCode(string code, int status)
    {
        var problem = Handle(new InvoiceEmailException(code, "Cannot draft the email"));

        problem.Status.ShouldBe(status);
        problem.Extensions["code"].ShouldBe(code);
    }

    [Test]
    public void ArgumentException_FromAnEntity_Is400()
    {
        var problem = Handle(new ArgumentException("Template name cannot be empty", "name"));

        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [TestCase(typeof(ArgumentNullException))]
    [TestCase(typeof(InvalidOperationException))]
    [TestCase(typeof(KeyNotFoundException))]
    [TestCase(typeof(NullReferenceException))]
    public void UnexpectedException_Is500_WithoutLeakingItsMessage(Type type)
    {
        var exception = (Exception)Activator.CreateInstance(type, "internal detail: connection string xyz")!;

        var problem = Handle(exception, Environments.Production);

        problem.Status.ShouldBe(StatusCodes.Status500InternalServerError);
        problem.Detail.ShouldBeNull();
        problem.Extensions["error"]!.ToString()!.ShouldNotContain("internal detail");
    }

    [Test]
    public void UnexpectedException_InDevelopment_ShowsTheExceptionInDetail()
    {
        var problem = Handle(new InvalidOperationException("boom"), Environments.Development);

        problem.Status.ShouldBe(StatusCodes.Status500InternalServerError);
        problem.Detail.ShouldNotBeNull().ShouldContain("boom");
    }

    [Test]
    public void MessageOf_ABodyThatDoesNotBind_JoinsTheFieldMessages()
    {
        // The automatic 400 of [ApiController] gets "error" from the AddProblemDetails hook
        var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["exportVersion"] = ["The ExportVersion field is required."],
            ["exportDate"] = ["The ExportDate field is required"]
        }) { Title = "One or more validation errors occurred." };

        ApiExceptionFilter.MessageOf(problem).ShouldBe("The ExportVersion field is required. The ExportDate field is required.");
    }

    [Test]
    public void MessageOf_OtherProblems_IsTheDetailOrTheTitle()
    {
        ApiExceptionFilter.MessageOf(new ProblemDetails { Title = "Not Found", Detail = "Invoice 9 not found" }).ShouldBe("Invoice 9 not found");
        ApiExceptionFilter.MessageOf(new ProblemDetails { Title = "Not Found" }).ShouldBe("Not Found");
    }

    [Test]
    public void CancelledRequest_IsLeftAlone()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var context = CreateContext(new OperationCanceledException(aborted.Token));
        context.HttpContext.RequestAborted = aborted.Token;

        Filter(Environments.Production).OnException(context);

        context.ExceptionHandled.ShouldBeFalse();
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

    private static ProblemDetails Handle(Exception exception, string environment = "Production")
    {
        var context = CreateContext(exception);

        Filter(environment).OnException(context);

        context.ExceptionHandled.ShouldBeTrue();
        var result = context.Result.ShouldBeOfType<ObjectResult>();
        result.ContentTypes.ShouldContain("application/problem+json");
        var problem = result.Value.ShouldBeAssignableTo<ProblemDetails>()!;
        result.StatusCode.ShouldBe(problem.Status);
        return problem;
    }

    private static ApiExceptionFilter Filter(string environment) =>
        new(NullLogger<ApiExceptionFilter>.Instance, Mock.Of<IHostEnvironment>(e => e.EnvironmentName == environment));

    private static ExceptionContext CreateContext(Exception exception)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddControllers();
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ExceptionContext(actionContext, []) { Exception = exception };
    }
}
