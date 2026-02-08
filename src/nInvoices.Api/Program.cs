using Serilog;
using nInvoices.Application;
using nInvoices.Core.Configuration;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.TaxHandlers;
using nInvoices.Infrastructure.TemplateEngine;
using nInvoices.Infrastructure.PdfExport;
using FluentValidation;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/nInvoices-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

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
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        var keycloakAuthority = builder.Configuration["Keycloak:Authority"] 
            ?? throw new InvalidOperationException("Keycloak:Authority not configured");
        var keycloakAudience = builder.Configuration["Keycloak:Audience"] 
            ?? throw new InvalidOperationException("Keycloak:Audience not configured");

        options.Authority = keycloakAuthority;
        
        // Allow configuration override for RequireHttpsMetadata
        // In Docker deployments, Keycloak may be accessed via HTTP internally
        var requireHttpsMetadata = builder.Configuration.GetValue<bool?>("Keycloak:RequireHttpsMetadata");
        options.RequireHttpsMetadata = requireHttpsMetadata ?? !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        
        // Use custom backchannel handler to rewrite localhost:8080 to keycloak:8080
        options.BackchannelHttpHandler = new nInvoices.Api.Infrastructure.KeycloakBackchannelHandler();
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAudiences = new[] { keycloakAudience, "ninvoices-web", "account" },
            // Accept internal, external, and public issuers
            ValidIssuers = new[] 
            { 
                "http://localhost:8080/realms/ninvoices",
                keycloakAuthority,
                builder.Configuration["Keycloak:ExternalAuthority"] ?? "",
                "http://DOMAIN_PLACEHOLDER:8080/realms/ninvoices"
            }.Where(s => !string.IsNullOrEmpty(s)).ToArray(),
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




