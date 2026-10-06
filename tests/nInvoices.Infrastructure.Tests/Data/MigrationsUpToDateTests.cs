using Microsoft.EntityFrameworkCore;
using nInvoices.Infrastructure.Data;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Data;

/// <summary>
/// Each provider has its own migrations. A change to the model (an entity, a configuration) needs a
/// migration in both, or one database would not get it; these fail until both exist. No database needed.
/// </summary>
[TestFixture]
public sealed class MigrationsUpToDateTests
{
    [Test]
    public void PostgreSqlMigrations_CoverTheModel()
    {
        using var context = Context(o => o.UseNpgsql("Host=unused", n => n.MigrationsAssembly(DatabaseExtensions.PostgreSqlMigrationsAssembly)));

        context.Database.HasPendingModelChanges().ShouldBeFalse(
            "The model changed without a PostgreSQL migration: dotnet ef migrations add <Name> " +
            "-p src/nInvoices.Infrastructure.Migrations.PostgreSql -s src/nInvoices.Infrastructure.Migrations.PostgreSql --context ApplicationDbContext");
    }

    [Test]
    public void SqliteMigrations_CoverTheModel()
    {
        using var context = Context(o => o.UseSqlite("Data Source=:memory:", s => s.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        context.Database.HasPendingModelChanges().ShouldBeFalse(
            "The model changed without a SQLite migration: dotnet ef migrations add <Name> " +
            "-p src/nInvoices.Infrastructure -s src/nInvoices.Api --context ApplicationDbContext");
    }

    private static ApplicationDbContext Context(Action<DbContextOptionsBuilder<ApplicationDbContext>> provider)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        provider(options);
        return new ApplicationDbContext(options.Options, TestEncryption.Encryptor, new TestUserContext(null));
    }
}
