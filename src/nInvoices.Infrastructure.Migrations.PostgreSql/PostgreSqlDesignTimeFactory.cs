using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
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

    public ApplicationDbContext CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=ninvoices_design_time", o => o.MigrationsAssembly(AssemblyName))
                .Options,
            FieldEncryptor.CreateEphemeral());
}
