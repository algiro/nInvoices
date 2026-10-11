using Microsoft.Extensions.Logging;
using nInvoices.Application.Compliance;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.ImportExport;
using nInvoices.Infrastructure.Data;

namespace nInvoices.Infrastructure.DataPortability;

/// <summary>
/// Exports and imports the user's data as JSON, for backups and moving between installations:
/// customers (with rates, taxes, templates, projects, worked days and unbilled expenses), invoices,
/// settings and assets, and the tax prediction's records (with the settings). Imports keep what the user already has and report per item what was
/// imported, skipped or failed. Works on the DbContext directly: it is bulk data transfer. Each kind
/// of data has its own class; this one puts them together.
/// </summary>
public sealed class DataPortabilityService : IDataPortability
{
    private readonly CustomerPortability _customers;
    private readonly SharedTemplatesPortability _sharedTemplates;
    private readonly InvoicePortability _invoices;
    private readonly SettingsPortability _settings;
    private readonly TaxRecordsPortability _taxes;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DataPortabilityService> _logger;

    public DataPortabilityService(
        ApplicationDbContext context,
        IComplianceRegistry compliance,
        TimeProvider timeProvider,
        ILogger<DataPortabilityService> logger)
    {
        _customers = new CustomerPortability(context, timeProvider, logger);
        _sharedTemplates = new SharedTemplatesPortability(context);
        _invoices = new InvoicePortability(context, timeProvider, logger);
        _settings = new SettingsPortability(context, compliance, timeProvider, logger);
        _taxes = new TaxRecordsPortability(context, logger);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<DataExportDto> ExportCustomersAsync(CancellationToken cancellationToken)
    {
        var customers = await _customers.ExportAsync(cancellationToken);
        // Shared templates have no customer: they are exported on their own
        var shared = await _sharedTemplates.ExportAsync(cancellationToken);

        return new DataExportDto("1.0", _timeProvider.GetUtcNow().UtcDateTime, customers, null, shared);
    }

    public Task<DataExportDto> ExportInvoicesAsync(int? year, int? month, long? customerId, CancellationToken cancellationToken) =>
        _invoices.ExportAsync(year, month, customerId, cancellationToken);

    public async Task<DataExportDto> ExportSettingsAsync(CancellationToken cancellationToken)
    {
        var export = await _settings.ExportAsync(cancellationToken);
        return export with { Settings = export.Settings! with { Taxes = await _taxes.ExportAsync(cancellationToken) } };
    }

    public async Task<ImportResultDto> ImportCustomersAsync(DataExportDto data, CancellationToken cancellationToken)
    {
        var (imported, skipped, errors) = await _customers.ImportAsync(data.Customers ?? [], cancellationToken);

        if (data.SharedTemplates is not null)
        {
            try
            {
                var (sharedImported, sharedSkipped) = await _sharedTemplates.ImportAsync(data.SharedTemplates, cancellationToken);
                imported += sharedImported;
                skipped += sharedSkipped;
            }
            catch (Exception ex)
            {
                errors.Add($"Shared templates: {ex.Message}");
                _logger.LogError(ex, "Failed to import the shared templates");
            }
        }

        _logger.LogInformation("Import complete: {Imported} imported, {Skipped} skipped, {Errors} errors",
            imported, skipped, errors.Count);

        return new ImportResultDto(imported, skipped, errors);
    }

    public Task<ImportResultDto> ImportInvoicesAsync(DataExportDto data, CancellationToken cancellationToken) =>
        _invoices.ImportAsync(data, cancellationToken);

    public async Task<ImportResultDto> ImportSettingsAsync(DataExportDto data, CancellationToken cancellationToken)
    {
        var settings = await _settings.ImportAsync(data, cancellationToken);
        if (data.Settings?.Taxes is not { } taxes)
            return settings;

        var taxRecords = await _taxes.ImportAsync(taxes, cancellationToken);
        return new ImportResultDto(
            settings.Imported + taxRecords.Imported,
            settings.Skipped + taxRecords.Skipped,
            [.. settings.Errors, .. taxRecords.Errors]);
    }
}
