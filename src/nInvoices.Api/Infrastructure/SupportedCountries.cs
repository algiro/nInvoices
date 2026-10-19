using System.Reflection;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection.Extensions;
using nInvoices.Application;
using nInvoices.Application.Behaviors;
using nInvoices.Application.Localization;
using nInvoices.Countries.Germany;
using nInvoices.Countries.Italy;
using nInvoices.Countries.Spain;
using nInvoices.Countries.UnitedKingdom;
using nInvoices.Infrastructure.Countries;

namespace nInvoices.Api.Infrastructure;

/// <summary>
/// The countries this build supports, each a plugin in its own project (<c>nInvoices.Countries.*</c>), and the
/// registration of what they bring. Adding a country: reference its project, add its plugin to <see cref="All"/>
/// and its assembly to the Mediator assemblies in <see cref="AddApplicationRequests"/> (the source generator
/// needs them written out; <c>SupportedCountriesTests</c> checks the two lists agree).
/// </summary>
public static class SupportedCountries
{
    public static IReadOnlyList<ICountryPlugin> All { get; } = [new SpainCountry(), new ItalyCountry(), new GermanyCountry(), new UnitedKingdomCountry()];

    /// <summary>The assemblies with request handlers and validators: the Application layer's and each country's.</summary>
    public static IReadOnlyList<Assembly> RequestAssemblies { get; } =
        [typeof(ApplicationAssemblyMarker).Assembly, .. All.Select(c => c.GetType().Assembly)];

    /// <summary>
    /// Registers each country's services and the plugins themselves (the DbContext takes their entities from
    /// them). Whether the installation offers a country is configuration (<c>Compliance:Countries</c>), not this.
    /// </summary>
    public static IServiceCollection AddCountries(this IServiceCollection services, IConfiguration configuration)
    {
        foreach (var country in All)
        {
            services.AddSingleton(country);
            country.AddServices(services, configuration);
        }
        return services;
    }

    /// <summary>Adds the controllers of each country's assembly.</summary>
    public static IMvcBuilder AddCountryControllers(this IMvcBuilder mvc)
    {
        foreach (var country in All)
            mvc.AddApplicationPart(country.GetType().Assembly);
        return mvc;
    }

    /// <summary>
    /// Registers the mediator with the handlers of the Application layer and of every country (wired at compile
    /// time by the Mediator source generator) and their FluentValidation validators, with the pipeline that runs
    /// a request's validators before its handler.
    /// </summary>
    public static IServiceCollection AddApplicationRequests(this IServiceCollection services)
    {
        services.AddMediator((MediatorOptions options) =>
        {
            options.Assemblies = [typeof(ApplicationAssemblyMarker), typeof(SpainCountry), typeof(ItalyCountry), typeof(GermanyCountry), typeof(UnitedKingdomCountry)];
            // Handlers use the request's DbContext and user: never the generator's default, Singleton
            options.ServiceLifetime = ServiceLifetime.Scoped;
            // The first one wraps the rest: metrics see the whole request, validation included
            options.PipelineBehaviors = [typeof(MetricsBehavior<,>), typeof(ValidationBehavior<,>)];
        });
        services.AddValidatorsFromAssemblies(RequestAssemblies);
        // The validators' messages, in the user's language (also registered by AddApplicationServices)
        services.TryAddSingleton<IAppText>(_ => new AppText());
        return services;
    }
}
