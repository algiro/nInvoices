using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using nInvoices.Core.Interfaces;
using nInvoices.Infrastructure.Data.Repositories;
using nInvoices.Infrastructure.Services;

namespace nInvoices.Infrastructure.Data;

/// <summary>
/// Extension methods for configuring database services.
/// Supports easy switching between SQLite, PostgreSQL, and SQL Server.
/// </summary>
public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbType = configuration["Database:Type"] ?? "SQLite";
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' not found");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            ConfigureDatabase(options, dbType, connectionString);
            
            // Enable sensitive data logging in development
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
            
            // Suppress pending model changes warning for PostgreSQL
            if (dbType == "PostgreSQL")
            {
                options.ConfigureWarnings(warnings => 
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            }
        });

        // Register Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register generic repository
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        
        // Register User Context
        services.AddScoped<IUserContext, UserContext>();

        return services;
    }

    private static void ConfigureDatabase(
        DbContextOptionsBuilder options,
        string dbType,
        string connectionString)
    {
        switch (dbType.ToUpperInvariant())
        {
            case "SQLITE":
                options.UseSqlite(connectionString, sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                });
                break;

            case "POSTGRESQL":
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                });
                break;

            case "SQLSERVER":
                throw new NotSupportedException($"Database type '{dbType}' requires additional NuGet packages. " +
                    $"Install EntityFrameworkCore.{dbType} package and uncomment the corresponding code.");

            default:
                throw new NotSupportedException($"Database type '{dbType}' is not supported. " +
                    "Supported types: SQLite, PostgreSQL (with package), SQLServer (with package)");
        }
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }
}
