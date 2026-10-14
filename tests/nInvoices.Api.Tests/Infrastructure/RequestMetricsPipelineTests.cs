using System.Diagnostics.Metrics;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using nInvoices.Application;
using nInvoices.Application.Features.BusinessExpenses;
using nInvoices.Application.Telemetry;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Api.Tests.Infrastructure;

/// <summary>The registered Mediator pipeline measures every request, and sees the ones a validator refuses.</summary>
[TestFixture]
public sealed class RequestMetricsPipelineTests
{
    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [Test]
    public async Task Pipeline_MeasuresSuccessAndValidationFailure_OfARealRequest()
    {
        var expenses = new Mock<IRepository<BusinessExpense>>();
        expenses.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<BusinessExpense, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(expenses.Object);
        services.AddApplicationRequests();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var outcomes = new List<string?>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == AppMetrics.MeterName && instrument.Name == "ninvoices.request.duration")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
        {
            var tagged = tags.ToArray().ToDictionary(t => t.Key, t => t.Value as string);
            if (tagged["request"] == nameof(GetBusinessExpensesQuery))
                lock (outcomes)
                    outcomes.Add(tagged["outcome"]);
        });
        listener.Start();

        await mediator.Send(new GetBusinessExpensesQuery(2026), Token);
        // Metrics is the outermost behaviour: a request refused by its validator is still measured
        await Should.ThrowAsync<ValidationException>(async () => await mediator.Send(new GetBusinessExpensesQuery(1), Token));

        outcomes.ShouldBe(["success", "invalid"], ignoreOrder: true);
    }
}
