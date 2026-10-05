using FluentValidation;
using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Customers.Commands;
using nInvoices.Application.Features.Customers.Validators;
using nInvoices.Application.Features.Invoices.Commands;
using nInvoices.Application.Features.Invoices.Queries;
using nInvoices.Application.Features.Invoices.Validators;
using nInvoices.Application.Features.Taxes.Commands;
using nInvoices.Application.Features.Taxes.Validators;
using nInvoices.Core.Enums;
using Shouldly;

namespace nInvoices.Application.Tests.Features;

[TestFixture]
public sealed class CommandValidatorsTests
{
    // Requests that carry a validated DTO but check it themselves instead of in the pipeline
    private static readonly HashSet<Type> ValidatedByTheirHandler =
    [
        // The wizard previews incomplete input: problems come back in the preview, not as a 400
        typeof(PreviewInvoiceDraftQuery)
    ];

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [Test]
    public void EveryRequestCarryingAValidatedDto_HasAValidator()
    {
        var assembly = typeof(ApplicationAssemblyMarker).Assembly;
        var validatedTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract)
            .Select(t => ValidatedType(t))
            .OfType<Type>()
            .ToHashSet();

        var missing = assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.GetInterfaces().Any(IsRequest))
            .Where(t => !ValidatedByTheirHandler.Contains(t))
            .Where(t => t.GetProperties().Any(p => validatedTypes.Contains(p.PropertyType)))
            .Where(t => !validatedTypes.Contains(t))
            .Select(t => t.Name)
            .ToList();

        missing.ShouldBeEmpty("These requests carry a DTO that has a validator, but nothing validates the request");
    }

    [Test]
    public async Task CreateTaxCommand_WithoutTaxId_IsValid()
    {
        // The tax form has no Tax ID field: the handler derives one from the description
        var command = new CreateTaxCommand(new CreateTaxDto(1, "", "VAT", "PERCENTAGE", 21m, TaxApplicationType.OnSubtotal, Order: 1));

        var result = await new CreateTaxCommandValidator().ValidateAsync(command, Token);

        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public async Task GenerateInvoiceCommand_MonthlyWithNoWorkDays_IsValid()
    {
        // A fixed monthly rate bills its price without any worked day
        var command = new GenerateInvoiceCommand(Monthly(workDays: []));

        var result = await new GenerateInvoiceCommandValidator().ValidateAsync(command, Token);

        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public async Task GenerateInvoiceCommand_MonthlyWithoutWorkDaysList_IsInvalid()
    {
        var command = new GenerateInvoiceCommand(Monthly(workDays: null));

        var result = await new GenerateInvoiceCommandValidator().ValidateAsync(command, Token);

        result.Errors.Select(e => e.PropertyName).ShouldBe(["Invoice.WorkDays"]);
    }

    [Test]
    public async Task UpdateInvoiceCommand_WithRenderedContentEmbeddingImages_IsValid()
    {
        // A rendered invoice carries its logo as a base64 data URI: a 1 MB image is ~1.4M characters
        var html = "<img src=\"data:image/png;base64," + new string('A', 1_400_000) + "\" />";
        var command = new UpdateInvoiceCommand(1, new UpdateInvoiceDto { RenderedContent = html });

        var result = await new UpdateInvoiceCommandValidator().ValidateAsync(command, Token);

        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public async Task CreateCustomerCommand_WithInvalidCustomer_ReportsTheDtoFields()
    {
        var command = new CreateCustomerCommand(new CreateCustomerDto(
            "", "B12345678", new AddressDto("Main St", "1", "", "28001", "ES")));

        var result = await new CreateCustomerCommandValidator().ValidateAsync(command, Token);

        result.Errors.Select(e => e.PropertyName).ShouldBe(["Customer.Name", "Customer.Address.City"], ignoreOrder: true);
    }

    [Test]
    public async Task CreateCustomerCommand_WithoutCustomer_IsInvalid()
    {
        var result = await new CreateCustomerCommandValidator().ValidateAsync(new CreateCustomerCommand(null!), Token);

        result.IsValid.ShouldBeFalse();
    }

    private static GenerateInvoiceDto Monthly(ICollection<WorkDayDto>? workDays) => new()
    {
        CustomerId = 1,
        InvoiceType = InvoiceType.Monthly,
        IssueDate = new DateOnly(2026, 10, 1),
        Year = 2026,
        Month = 9,
        WorkDays = workDays
    };

    private static bool IsRequest(Type type) =>
        type == typeof(IRequest) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRequest<>));

    // T when the type is an AbstractValidator<T>
    private static Type? ValidatedType(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
                return current.GetGenericArguments()[0];
        }

        return null;
    }
}
