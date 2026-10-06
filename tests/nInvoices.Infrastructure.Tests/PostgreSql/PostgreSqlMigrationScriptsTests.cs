using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.Tests.Data;
using Shouldly;
using Testcontainers.PostgreSql;

namespace nInvoices.Infrastructure.Tests.PostgreSql;

/// <summary>
/// Production runs on PostgreSQL and gets its schema changes from the hand-written psql scripts in
/// docker/migrations-postgres, while fresh installs build the schema from the EF model. These tests
/// run the scripts with psql, as deploy.sh does, on a real PostgreSQL (Testcontainers; needs Docker,
/// skipped without it), so the two can't silently drift apart.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
[NonParallelizable]
public sealed class PostgreSqlMigrationScriptsTests
{
    // The scripts \connect to it by name
    private const string Database = "ninvoices_db";

    // A second database, built from the EF model, to compare with
    private const string ModelDatabase = "model_db";

    private PostgreSqlContainer? _container;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    // No pooling: each test drops and re-creates the database, which ends the server side of pooled connections
    private string ServerConnectionString =>
        new NpgsqlConnectionStringBuilder(_container!.GetConnectionString()) { Pooling = false }.ConnectionString;

    private string ConnectionString =>
        new NpgsqlConnectionStringBuilder(ServerConnectionString) { Database = Database }.ConnectionString;

    [OneTimeSetUp]
    public async Task StartPostgreSql()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine").Build(); // as in production
            await _container.StartAsync(Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Assert.Ignore($"PostgreSQL tests need Docker: {ex.Message}");
        }
    }

    [OneTimeTearDown]
    public async Task StopPostgreSql()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    [SetUp]
    public async Task EmptyDatabase()
    {
        await PostgreSqlSchema.ExecuteAsync(ServerConnectionString, $"DROP DATABASE IF EXISTS {Database} WITH (FORCE)", Token);
        await PostgreSqlSchema.ExecuteAsync(ServerConnectionString, $"CREATE DATABASE {Database}", Token);
    }

    [Test]
    public async Task Production_AfterTheScripts_MatchesTheModel()
    {
        // The schema production has (PostgreSql/production-schema.sql, from pg_dump), brought up to date
        // the way deploy.sh --migrate does, must be the schema the EF model describes
        await RunPsqlAsync(Fixture("production-schema.sql"));
        foreach (var script in Scripts())
            await RunPsqlAsync(script);

        PostgreSqlSchema.Diff(await ModelSnapshotAsync(), await PostgreSqlSchema.SnapshotAsync(ConnectionString, Token)).ShouldBeEmpty();
    }

    [Test]
    public async Task EfMigrations_OnAnEmptyDatabase_BuildExactlyTheModelSchema()
    {
        // A fresh install: the API applies every migration at startup
        await using (var context = MigrationsContext(ConnectionString))
            await context.Database.MigrateAsync(Token);

        PostgreSqlSchema.Diff(await ModelSnapshotAsync(), await PostgreSqlSchema.SnapshotAsync(ConnectionString, Token)).ShouldBeEmpty();
    }

    [Test]
    public async Task EfMigrations_OnProduction_HaveNothingToApply()
    {
        // Production now: its schema, and the (frozen) scripts, the last of which records the baseline
        await RunPsqlAsync(Fixture("production-schema.sql"));
        foreach (var script in Scripts())
            await RunPsqlAsync(script);

        await using var context = MigrationsContext(ConnectionString);
        (await context.Database.GetPendingMigrationsAsync(Token)).ShouldBeEmpty();
    }

    [Test]
    public async Task Alignment_KeepsTheRows_ContinuesTheIds_AndMakesAliasesUniquePerUser()
    {
        // Production as it was before the alignment (applied 2026-10-06): its schema and the earlier scripts, with rows in it
        await RunPsqlAsync(Fixture("production-schema-before-alignment.sql"));
        var alignment = Scripts().Single(s => s.EndsWith("_align-with-ef-model.sql", StringComparison.Ordinal));
        foreach (var script in Scripts().Where(s => s != alignment))
            await RunPsqlAsync(script);
        await PostgreSqlSchema.ExecuteAsync(ConnectionString, """
            INSERT INTO "ImageAssets" ("Alias", "FileName", "ContentType", "Base64Data", "FileSize", "OwnerId")
            VALUES ('logo', 'logo.png', 'image/png', 'AAAA', 3, 'user-a'), ('brand', 'brand.png', 'image/png', 'BBBB', 3, 'user-a');
            DELETE FROM "ImageAssets" WHERE "Alias" = 'brand';
            """, Token);

        await RunPsqlAsync(alignment);

        // The row is still there, and the next id comes after the highest one ever handed out (2, deleted)
        (await PostgreSqlSchema.ScalarAsync<string>(ConnectionString, """SELECT "Alias" FROM "ImageAssets" WHERE "Id" = 1""", Token)).ShouldBe("logo");
        var next = await PostgreSqlSchema.ScalarAsync<long>(ConnectionString, """
            INSERT INTO "ImageAssets" ("Alias", "FileName", "ContentType", "Base64Data", "FileSize", "OwnerId", "CreatedAt")
            VALUES ('logo', 'logo.png', 'image/png', 'CCCC', 3, 'user-b', now()) RETURNING "Id"
            """, Token);
        next.ShouldBe(3);

        // Another user's "logo" was just accepted; the same user's second "logo" is not
        var duplicate = await Should.ThrowAsync<PostgresException>(() => PostgreSqlSchema.ExecuteAsync(ConnectionString, """
            INSERT INTO "ImageAssets" ("Alias", "FileName", "ContentType", "Base64Data", "FileSize", "OwnerId", "CreatedAt")
            VALUES ('logo', 'logo.png', 'image/png', 'DDDD', 3, 'user-a', now())
            """, Token));
        duplicate.ConstraintName.ShouldBe("IX_ImageAssets_OwnerId_Alias");
    }

    [Test]
    public async Task Scripts_OnASchemaBuiltFromTheModel_RunAndChangeNothing()
    {
        // A fresh install builds the schema from the EF model (Database:EnsureCreated); deploy.sh --migrate
        // then runs every script on it. They must succeed (idempotent) and add nothing the model lacks
        await CreateFromModelAsync();
        var fromModel = await PostgreSqlSchema.SnapshotAsync(ConnectionString, Token);

        foreach (var script in Scripts())
            await RunPsqlAsync(script);

        PostgreSqlSchema.Diff(fromModel, await PostgreSqlSchema.SnapshotAsync(ConnectionString, Token)).ShouldBeEmpty();
    }

    [Test]
    public async Task Scripts_RunTwice_AreIdempotent()
    {
        await CreateFromModelAsync();
        foreach (var script in Scripts())
            await RunPsqlAsync(script);
        var once = await PostgreSqlSchema.SnapshotAsync(ConnectionString, Token);

        foreach (var script in Scripts())
            await RunPsqlAsync(script);

        PostgreSqlSchema.Diff(once, await PostgreSqlSchema.SnapshotAsync(ConnectionString, Token)).ShouldBeEmpty();
    }

    private async Task<IReadOnlyList<string>> ModelSnapshotAsync()
    {
        await PostgreSqlSchema.ExecuteAsync(ServerConnectionString, $"DROP DATABASE IF EXISTS {ModelDatabase} WITH (FORCE)", Token);
        await PostgreSqlSchema.ExecuteAsync(ServerConnectionString, $"CREATE DATABASE {ModelDatabase}", Token);
        var connectionString = new NpgsqlConnectionStringBuilder(ServerConnectionString) { Database = ModelDatabase }.ConnectionString;
        await CreateFromModelAsync(connectionString);
        return await PostgreSqlSchema.SnapshotAsync(connectionString, Token);
    }

    private Task CreateFromModelAsync() => CreateFromModelAsync(ConnectionString);

    /// <summary>A context as production builds it: Npgsql with the PostgreSQL migrations.</summary>
    private static ApplicationDbContext MigrationsContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString, o => o.MigrationsAssembly(DatabaseExtensions.PostgreSqlMigrationsAssembly))
                .Options,
            TestEncryption.Encryptor,
            new TestUserContext(null));

    private static async Task CreateFromModelAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, o => o.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;
        await using var context = new ApplicationDbContext(options, TestEncryption.Encryptor, new TestUserContext(null));
        await context.Database.EnsureCreatedAsync(Token);
    }

    /// <summary>Runs a script with psql in the container, stopping at the first error like deploy.sh.</summary>
    private async Task RunPsqlAsync(string script)
    {
        var target = "/tmp/" + Path.GetFileName(script);
        await _container!.CopyAsync(Encoding.UTF8.GetBytes(await File.ReadAllTextAsync(script, Token)), target, ct: Token);
        var result = await _container.ExecAsync(
            ["psql", "-v", "ON_ERROR_STOP=1", "-U", "postgres", "-d", Database, "-f", target], Token);

        if (result.ExitCode != 0)
            Assert.Fail($"{Path.GetFileName(script)} failed (psql exit {result.ExitCode}): {result.Stderr.Trim()}");
    }

    /// <summary>docker/migrations-postgres/*.sql in the order deploy.sh runs them (file name).</summary>
    private static IReadOnlyList<string> Scripts() =>
        Directory.GetFiles(Path.Combine(RepositoryRoot(), "docker", "migrations-postgres"), "*.sql")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

    private static string Fixture(string name) =>
        Path.Combine(RepositoryRoot(), "tests", "nInvoices.Infrastructure.Tests", "PostgreSql", name);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "docker", "migrations-postgres")))
            directory = directory.Parent;
        directory.ShouldNotBeNull("the repository root (with docker/migrations-postgres) not found above the test directory");
        return directory.FullName;
    }
}
