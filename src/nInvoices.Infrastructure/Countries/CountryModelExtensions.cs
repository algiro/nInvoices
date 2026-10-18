using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using nInvoices.Infrastructure.Data;

namespace nInvoices.Infrastructure.Countries;

public static class CountryModelExtensions
{
    /// <summary>
    /// Adds to the <see cref="ApplicationDbContext"/> model the entity configurations
    /// (<see cref="IEntityTypeConfiguration{TEntity}"/>) of the given country assemblies.
    /// </summary>
    public static TBuilder UseCountryModels<TBuilder>(this TBuilder builder, IEnumerable<Assembly> assemblies)
        where TBuilder : DbContextOptionsBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assemblies);

        var extension = new CountryModelExtension(assemblies.Distinct().OrderBy(a => a.FullName, StringComparer.Ordinal).ToList());
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(extension);
        return builder;
    }

    /// <inheritdoc cref="UseCountryModels{TBuilder}(TBuilder, IEnumerable{Assembly})"/>
    public static TBuilder UseCountryModels<TBuilder>(this TBuilder builder, IEnumerable<ICountryPlugin> plugins)
        where TBuilder : DbContextOptionsBuilder
    {
        ArgumentNullException.ThrowIfNull(plugins);
        return builder.UseCountryModels(plugins.Select(p => p.GetType().Assembly));
    }
}

/// <summary>The country assemblies whose entities are part of the model.</summary>
internal sealed class CountryModelExtension : IDbContextOptionsExtension
{
    public CountryModelExtension(IReadOnlyList<Assembly> assemblies)
    {
        Assemblies = assemblies;
        Key = string.Join(";", assemblies.Select(a => a.GetName().Name));
        Info = new ExtensionInfo(this);
    }

    public IReadOnlyList<Assembly> Assemblies { get; }

    /// <summary>Tells models apart in the cache: a context with other countries has another model.</summary>
    public string Key { get; }

    public DbContextOptionsExtensionInfo Info { get; }

    public void ApplyServices(IServiceCollection services)
    {
    }

    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class ExtensionInfo(CountryModelExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        private new CountryModelExtension Extension => (CountryModelExtension)base.Extension;

        public override bool IsDatabaseProvider => false;

        public override string LogFragment => $"Countries={Extension.Key} ";

        public override int GetServiceProviderHashCode() => StringComparer.Ordinal.GetHashCode(Extension.Key);

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) =>
            other is ExtensionInfo info && info.Extension.Key == Extension.Key;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) =>
            debugInfo["Countries"] = Extension.Key;
    }
}
