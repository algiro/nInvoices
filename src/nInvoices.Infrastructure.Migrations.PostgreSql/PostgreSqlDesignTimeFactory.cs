using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using nInvoices.Countries.Spain;
using nInvoices.Infrastructure.Countries;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.Encryption;

namespace nInvoices.Infrastructure.Migrations.PostgreSql;

/// <summary>
/// Lets <c>dotnet ef</c> build the PostgreSQL model without the API's configuration (which is SQLite in
/// development). Only used to add migrations, never to connect:
/// <code>
/// dotnet ef migrations add &lt;Name&gt; -p src/nInvoices.Infrastructure.Migrations.PostgreSql -s src/nInvoices.Infrastructure.Migrations.PostgreSql --context ApplicationDbContext
/// </code>
/// </summary>
public sealed class PostgreSqlDesignTimeFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public const string AssemblyName = DatabaseExtensions.PostgreSqlMigrationsAssembly;

    /// <summary>The countries whose entities are in the model: every one the Api registers (<c>SupportedCountries.All</c>).</summary>
    public static IReadOnlyList<ICountryPlugin> Countries { get; } = [new SpainCountry()];

    public ApplicationDbContext CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=ninvoices_design_time", o => o.MigrationsAssembly(AssemblyName))
                .UseCountryModels(Countries)
                .Options,
            FieldEncryptor.CreateEphemeral());
}
