using Serilog;
using Microsoft.EntityFrameworkCore;
using nInvoices.Application;
using nInvoices.Core.Configuration;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.TaxHandlers;
using nInvoices.Infrastructure.TemplateEngine;
using nInvoices.Infrastructure.PdfExport;
using nInvoices.Infrastructure.Gmail;
using Microsoft.AspNetCore.DataProtection;
using FluentValidation;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

// Build-time hook (docker/Dockerfile.api): download the headless Chrome used for PDF export, then exit
if (args.Contains("--download-chrome"))
{
    await PuppeteerPdfConverter.EnsureBrowserDownloadedAsync();
    Console.WriteLine("Headless Chrome is ready for PDF export.");
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/nInvoices-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Aspire service defaults: OpenTelemetry (traces + metrics), "/health" + "/alive"
// health checks, HTTP resilience, and service discovery.
// Logs continue to flow through Serilog; they are exported over OTLP only once
// Serilog.Sinks.OpenTelemetry is added (Phase 3). Traces and metrics export
// automatically when OTEL_EXPORTER_OTLP_ENDPOINT is set.
builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Add Configuration
builder.Services.Configure<InvoiceSettings>(builder.Configuration.GetSection(InvoiceSettings.SectionName));

// Add HttpContextAccessor for user context
builder.Services.AddHttpContextAccessor();

// Add Database
builder.Services.AddDatabase(builder.Configuration);

// Add Tax Handlers
builder.Services.AddTaxHandlers();

// Add Template Engine
builder.Services.AddTemplateEngine();
builder.Services.AddPdfExport();

// Data Protection encrypts the Gmail refresh tokens stored in the database. Keys must survive
// restarts and redeploys (a Docker volume in production), otherwise stored tokens become unreadable.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "keys");
builder.Services.AddDataProtection()
    .SetApplicationName("nInvoices")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

// Gmail drafts for invoice emails (inactive until Gmail:ClientId/ClientSecret/RedirectUri are set)
builder.Services.AddGmail(builder.Configuration);

// Add Application Services
builder.Services.AddApplicationServices();

// Add MediatR for CQRS
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(nInvoices.Application.ApplicationAssemblyMarker).Assembly);
});

// Add FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(nInvoices.Application.ApplicationAssemblyMarker).Assembly);

// Configure Authentication & Authorization
var useDevAuth = builder.Configuration.GetValue<bool>("Authentication:UseDevAuth");

if (useDevAuth)
{
    Log.Warning("*** DEV AUTH ENABLED — all requests auto-authenticated as dev user ***");
    builder.Services.AddAuthentication(nInvoices.Api.Infrastructure.DevAuthenticationHandler.SchemeName)
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
            nInvoices.Api.Infrastructure.DevAuthenticationHandler>(
            nInvoices.Api.Infrastructure.DevAuthenticationHandler.SchemeName, _ => { });
}
else
{
    builder.Services.AddAuthentication("Bearer")
        .AddJwtBearer("Bearer", options =>
        {
            var keycloakAuthority = builder.Configuration["Keycloak:Authority"]
                ?? throw new InvalidOperationException("Keycloak:Authority not configured");
            var keycloakAudience = builder.Configuration["Keycloak:Audience"]
                ?? throw new InvalidOperationException("Keycloak:Audience not configured");

            var keycloakExternalAuthority = builder.Configuration["Keycloak:ExternalAuthority"];

            // Extract hostname from ExternalAuthority for backchannel URL rewriting
            var externalHost = keycloakExternalAuthority is not null
                ? new Uri(keycloakExternalAuthority).Host
                : null;

            options.Authority = keycloakAuthority;

            // Allow configuration override for RequireHttpsMetadata
            // In Docker deployments, Keycloak may be accessed via HTTP internally
            var requireHttpsMetadata = builder.Configuration.GetValue<bool?>("Keycloak:RequireHttpsMetadata");
            options.RequireHttpsMetadata = requireHttpsMetadata ?? !builder.Environment.IsDevelopment();
            options.SaveToken = true;

            // Rewrite external hostname requests to internal keycloak:8080
            options.BackchannelHttpHandler = new nInvoices.Api.Infrastructure.KeycloakBackchannelHandler(
                externalHost is not null ? [externalHost] : null);

            // Build valid issuers from config — Keycloak may present different issuer URLs
            // depending on whether accessed internally or externally
            var validIssuers = new List<string>
            {
                "http://localhost:8080/realms/ninvoices",
                keycloakAuthority
            };
            if (keycloakExternalAuthority is not null)
            {
                validIssuers.Add(keycloakExternalAuthority);
                // Keycloak internal metadata uses http://<hostname>:8080 as issuer
                validIssuers.Add($"http://{externalHost}:8080/realms/ninvoices");
            }

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidAudiences = new[] { keycloakAudience, "ninvoices-web", "account" },
                ValidIssuers = validIssuers.ToArray(),
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Log.Error("Authentication failed: {Error}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var userId = context.Principal?.FindFirst("sub")?.Value;
                    Log.Information("Token validated for user: {UserId}", userId);
                    return Task.CompletedTask;
                }
            };
        });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireUser", policy => policy.RequireRole("user"));
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("admin"));
});

// Configure CORS
var corsOrigins = builder.Configuration["Cors:Origins"]?.Split(',') 
    ?? ["http://localhost:5173", "http://localhost:5174", "http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Say at startup why Gmail drafts are unavailable, instead of only in the Settings page
var gmailOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<GmailOptions>>().Value;
if (!gmailOptions.IsConfigured)
{
    var missing = new[]
    {
        string.IsNullOrWhiteSpace(gmailOptions.ClientId) ? "Gmail:ClientId" : null,
        string.IsNullOrWhiteSpace(gmailOptions.ClientSecret) ? "Gmail:ClientSecret" : null,
        string.IsNullOrWhiteSpace(gmailOptions.RedirectUri) ? "Gmail:RedirectUri" : null
    }.OfType<string>();
    Log.Warning("Gmail drafts are off: {Missing} not configured (environment {Environment}, APPDATA {AppData})",
        string.Join(", ", missing), app.Environment.EnvironmentName, Environment.GetEnvironmentVariable("APPDATA"));
}

// Database:EnsureCreated builds the schema from the current EF model on an EMPTY database
// (fresh Docker installs, the Aspire AppHost); on a database that already has tables it does
// nothing. EF migrations are SQLite-scaffolded and cannot run under Npgsql, so later schema
// changes to an existing PostgreSQL database go through docker/migrations-postgres/*.sql.
if (app.Configuration.GetValue<bool>("Database:EnsureCreated"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
    Log.Information("Database:EnsureCreated — schema ensured from the EF model");
}

// Data is scoped per user. Rows created before that have no owner and stay invisible until
// MultiUser:LegacyOwnerId names the user (their "sub" claim) who should own them.
var legacyOwnerId = app.Configuration["MultiUser:LegacyOwnerId"];
var (assignedRows, unownedRows) = await app.Services.AssignUnownedDataAsync(legacyOwnerId);
if (assignedRows > 0)
    Log.Information("Assigned {Count} unowned rows to user {UserId} (MultiUser:LegacyOwnerId)", assignedRows, legacyOwnerId);
if (unownedRows > 0)
    Log.Warning("{Count} rows have no owner and are hidden from every user. Set MultiUser:LegacyOwnerId to the " +
        "user id that should own them (logged at sign-in as 'Token validated for user: <id>') and restart", unownedRows);

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors("AllowVueApp");
app.UseAuthentication();
app.UseAuthorization();

// "/health" (all checks incl. database) and "/alive" (liveness only).
// Anonymous; used by the container healthchecks.
app.MapDefaultEndpoints();

app.MapControllers();

try
{
    Log.Information("Starting nInvoices API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}




