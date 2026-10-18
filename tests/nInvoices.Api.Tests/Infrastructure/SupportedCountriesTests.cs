using Mediator;
using Microsoft.Extensions.DependencyInjection;
using nInvoices.Api.Infrastructure;
using nInvoices.Infrastructure.Migrations.PostgreSql;
using Shouldly;

namespace nInvoices.Api.Tests.Infrastructure;

/// <summary>
/// A country is listed in a few places the compiler can't tie together (the plugins, the Mediator source
/// generator's assemblies, the PostgreSQL design-time factory): these tests fail when one is forgotten.
/// </summary>
[TestFixture]
public sealed class SupportedCountriesTests
{
    [Test]
    public void EveryCountryRequest_HasItsHandlerRegistered()
    {
        // A country missing from the Mediator assemblies has requests nobody handles
        var services = new ServiceCollection().AddApplicationRequests();

        var requests = (
            from assembly in SupportedCountries.All.Select(c => c.GetType().Assembly)
            from type in assembly.GetTypes()
            where !type.IsAbstract
            from request in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
            select (Type: type, Handler: typeof(IRequestHandler<,>).MakeGenericType(type, request.GetGenericArguments()[0]))).ToList();

        requests.ShouldNotBeEmpty();
        requests.Where(r => services.All(d => d.ServiceType != r.Handler)).Select(r => r.Type.FullName).ShouldBeEmpty();
    }

    [Test]
    public void PostgreSqlDesignTimeModel_HasEveryCountry()
    {
        // Otherwise "dotnet ef migrations add" for PostgreSQL would drop the missing country's tables
        PostgreSqlDesignTimeFactory.Countries.Select(c => c.GetType())
            .ShouldBe(SupportedCountries.All.Select(c => c.GetType()), ignoreOrder: true);
    }

    [Test]
    public void CountryCodes_AreUniqueIsoCodes()
    {
        var codes = SupportedCountries.All.Select(c => c.CountryCode).ToList();

        codes.ShouldBeUnique();
        codes.ShouldAllBe(code => code.Length == 2 && code.All(char.IsAsciiLetterUpper));
    }
}
