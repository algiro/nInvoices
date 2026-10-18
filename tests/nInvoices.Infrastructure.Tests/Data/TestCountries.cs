using nInvoices.Countries.Spain;
using nInvoices.Infrastructure.Countries;

namespace nInvoices.Infrastructure.Tests.Data;

/// <summary>
/// The countries whose entities are in the model, as the Api registers them: the migrations create their
/// tables, so a context without them would see pending model changes.
/// </summary>
internal static class TestCountries
{
    public static IReadOnlyList<ICountryPlugin> All { get; } = [new SpainCountry()];
}
