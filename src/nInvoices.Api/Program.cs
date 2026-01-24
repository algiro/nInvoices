using Serilog;
using nInvoices.Application;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.TaxHandlers;
using nInvoices.Infrastructure.TemplateEngine;
using nInvoices.Infrastructure.PdfExport;
using FluentValidation;
using System.Text.Json.Serialization;

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

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173", 
                "http://localhost:5174", 
                "http://localhost:3000",
                "http://localhost:3001",
                "http://localhost:5297",
                "https://localhost:7277")
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




