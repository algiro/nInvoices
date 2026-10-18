using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace nInvoices.Infrastructure.Countries;

/// <summary>
/// Everything one country adds, in its own assembly (<c>nInvoices.Countries.*</c>): its invoicing rules
/// (<see cref="Core.Compliance.ICountryComplianceModule"/>), its tax prediction
/// (<see cref="Core.TaxPrediction.ITaxPredictionModule"/>), and what they need: entities, clients, workers,
/// endpoints. The Api lists the plugins; nothing else names a country.
/// </summary>
/// <remarks>
/// Besides <see cref="AddServices"/>, the plugin's assembly is searched for its EF Core configurations
/// (<see cref="CountryModelExtensions.UseCountryModels"/>), controllers, request handlers and validators.
/// The database model always includes every plugin's entities, whether or not the installation offers the
/// country, so that the migrations are the same everywhere.
/// </remarks>
public interface ICountryPlugin
{
    /// <summary>ISO 3166-1 alpha-2 code, upper case.</summary>
    string CountryCode { get; }

    /// <summary>Registers the country's modules and services, and binds its configuration sections.</summary>
    void AddServices(IServiceCollection services, IConfiguration configuration);
}
