using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Api.Controllers;
using nInvoices.Api.Infrastructure;
using nInvoices.Application;
using nInvoices.Core.Interfaces;
using nInvoices.Infrastructure.Data;
using Shouldly;

namespace nInvoices.Api.Tests;

/// <summary>
/// The layering the project is built on (Core ← Application ← Infrastructure ← Countries ← Api), checked so it
/// can't erode one shortcut at a time.
/// </summary>
[TestFixture]
public sealed class ArchitectureTests
{
    private static readonly Assembly Core = typeof(IRepository<>).Assembly;
    private static readonly Assembly ApplicationLayer = typeof(ApplicationAssemblyMarker).Assembly;
    private static readonly Assembly Infrastructure = typeof(ApplicationDbContext).Assembly;
    private static readonly Assembly Api = typeof(InvoicesController).Assembly;

    private static IReadOnlyList<Assembly> CountryAssemblies => SupportedCountries.All.Select(c => c.GetType().Assembly).ToList();

    // The Api's controllers and those the countries bring
    private static IEnumerable<Type> Controllers() =>
        CountryAssemblies.Prepend(Api)
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

    [Test]
    public void Controllers_DoNotReachTheDatabase()
    {
        // Data is reached through a request (IMediator); a controller handles HTTP only
        var violations =
            from controller in Controllers()
            from constructor in controller.GetConstructors()
            from parameter in constructor.GetParameters()
            where IsDataAccess(parameter.ParameterType)
            select $"{controller.Name}({parameter.ParameterType.Name} {parameter.Name})";

        violations.ShouldBeEmpty();
    }

    [Test]
    public void Layers_OnlyDependInwards()
    {
        References(Core).ShouldBeEmpty();
        References(ApplicationLayer).ShouldBe([Core.GetName().Name!]);
        References(Infrastructure).ShouldNotContain(Api.GetName().Name!);
    }

    [Test]
    public void SharedLayers_KnowNoCountry()
    {
        // A country is a plugin: the layers it plugs into never name it
        foreach (var layer in new[] { Core, ApplicationLayer, Infrastructure })
            References(layer).ShouldNotContain(name => name.StartsWith("nInvoices.Countries.", StringComparison.Ordinal), layer.GetName().Name);
    }

    [Test]
    public void Countries_DependOnlyOnTheSharedLayers()
    {
        // Not on the Api, nor on each other
        foreach (var country in CountryAssemblies)
            References(country).ShouldBeSubsetOf([Core.GetName().Name!, ApplicationLayer.GetName().Name!, Infrastructure.GetName().Name!], country.GetName().Name);
    }

    private static bool IsDataAccess(Type type) =>
        type.Assembly == Infrastructure
        || type == typeof(IUnitOfWork)
        || type.GetInterfaces().Append(type).Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRepository<>))
        || type == typeof(IAccountDataEraser);

    // The project's own assemblies an assembly references
    private static IReadOnlyList<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => name.StartsWith("nInvoices.", StringComparison.Ordinal))
            .Order()
            .ToList();
}
