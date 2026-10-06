using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.ImportExport;

/// <summary>
/// Moves the user's data in and out as JSON (backups, moving between installations). Implemented in
/// Infrastructure, over the database directly. Imports keep what the user already has and report per
/// item what was imported, skipped or failed instead of stopping at the first problem.
/// </summary>
public interface IDataPortability
{
    /// <summary>Customers with their rates, taxes, templates, projects, worked days and unbilled expenses, plus the shared templates.</summary>
    Task<DataExportDto> ExportCustomersAsync(CancellationToken cancellationToken);

    /// <summary>Invoices, optionally of one year, month and/or customer.</summary>
    Task<DataExportDto> ExportInvoicesAsync(int? year, int? month, long? customerId, CancellationToken cancellationToken);

    /// <summary>Invoice numbering, images, changed holiday calendars and e-invoicing settings (without the certificate).</summary>
    Task<DataExportDto> ExportSettingsAsync(CancellationToken cancellationToken);

    /// <summary>Customers not there yet (matched by fiscal id) and the shared templates.</summary>
    Task<ImportResultDto> ImportCustomersAsync(DataExportDto data, CancellationToken cancellationToken);

    /// <summary>Invoices of existing customers (matched by fiscal id) that are not there yet (matched by number).</summary>
    Task<ImportResultDto> ImportInvoicesAsync(DataExportDto data, CancellationToken cancellationToken);

    /// <summary>Settings and assets; the invoice sequence only moves forward.</summary>
    Task<ImportResultDto> ImportSettingsAsync(DataExportDto data, CancellationToken cancellationToken);
}
