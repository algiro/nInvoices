using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using nInvoices.Application.Compliance;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;

namespace nInvoices.Infrastructure.DataPortability;

/// <summary>
/// Settings and assets: invoice numbering, images used by templates, changed holiday calendars and
/// e-invoicing settings (without the signing certificate).
/// </summary>
internal sealed class SettingsPortability
{
    private readonly ApplicationDbContext _context;
    private readonly IComplianceRegistry _compliance;
    private readonly ILogger _logger;

    public SettingsPortability(ApplicationDbContext context, IComplianceRegistry compliance, ILogger logger)
    {
        _context = context;
        _compliance = compliance;
        _logger = logger;
    }

    /// <summary>
    /// Export the user's settings and assets: invoice numbering, images used by templates, the
    /// holiday calendars they changed and their e-invoicing settings (without the signing certificate).
    /// </summary>
    public async Task<DataExportDto> ExportAsync(CancellationToken cancellationToken)
    {
        var sequence = await _context.InvoiceSequences.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var images = await _context.ImageAssets.AsNoTracking().OrderBy(i => i.Id).ToListAsync(cancellationToken);
        var calendars = await _context.HolidayCalendars.Include(c => c.Rules).AsNoTracking()
            .OrderBy(c => c.CountryCode).ToListAsync(cancellationToken);
        var compliance = await _context.ComplianceSettings.AsNoTracking()
            .OrderBy(c => c.CountryCode).ToListAsync(cancellationToken);

        var settings = new UserSettingsExportDto(
            sequence is null ? null : new InvoiceNumberingExportDto(sequence.CurrentValue, sequence.NumberFormat),
            images.Select(i => new ImageAssetExportDto(i.Alias, i.FileName, i.ContentType, i.Base64Data, i.FileSize)).ToList(),
            calendars.Select(c => new HolidayCalendarExportDto(c.CountryCode, c.Rules.OrderBy(r => r.Id).Select(r =>
                new HolidayRuleExportDto(r.Name, r.Kind, r.Month, r.Day, r.EasterOffset, r.Weekday, r.Occurrence,
                    r.FromYear, r.ToYear, r.IsActive)).ToList())).ToList(),
            compliance.Select(c => new ComplianceSettingsExportDto(
                c.CountryCode,
                c.IsEnabled,
                c.LegalName,
                c.TaxId,
                c.Address is { } a ? new AddressDto(a.Street, a.HouseNumber, a.City, a.ZipCode, a.Country, a.State) : null,
                c.Values)).ToList());

        return new DataExportDto("1.0", DateTime.UtcNow, null, null, Settings: settings);
    }

    /// <summary>
    /// Import settings and assets. What the user already has is kept: an image with the same name, a
    /// calendar for the same country, e-invoicing settings for the same country. The invoice sequence
    /// only moves forward, so restoring never hands out a number twice.
    /// </summary>
    public async Task<ImportResultDto> ImportAsync(
        DataExportDto data,
        CancellationToken cancellationToken)
    {
        // Present: checked by ImportSettingsCommandValidator
        var settings = data.Settings!;

        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();

        async Task Try(string what, Func<Task<bool>> import)
        {
            try
            {
                if (await import())
                    imported++;
                else
                    skipped++;
            }
            catch (Exception ex)
            {
                // Whatever this item staged is dropped, so the next save doesn't retry it
                _context.ChangeTracker.Clear();
                errors.Add($"{what}: {ex.Message}");
                _logger.LogError(ex, "Failed to import {What}", what);
            }
        }

        if (settings.InvoiceNumbering is { } numbering)
            await Try("Invoice numbering", () => ImportNumberingAsync(numbering, cancellationToken));

        var aliases = (await _context.ImageAssets.AsNoTracking().Select(i => i.Alias).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var image in settings.Images ?? [])
        {
            await Try($"Image '{image.Alias}'", async () =>
            {
                if (!aliases.Add(image.Alias))
                    return false;
                await _context.ImageAssets.AddAsync(
                    new ImageAsset(image.Alias, image.FileName, image.ContentType, image.Base64Data, image.FileSize), cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            });
        }

        var countries = (await _context.HolidayCalendars.AsNoTracking().Select(c => c.CountryCode).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var calendarData in settings.HolidayCalendars ?? [])
        {
            await Try($"Holiday calendar {calendarData.CountryCode}", async () =>
            {
                var calendar = new HolidayCalendar(calendarData.CountryCode);
                if (!countries.Add(calendar.CountryCode))
                    return false;
                foreach (var r in calendarData.Rules)
                {
                    var rule = HolidayRule.Create(r.Name, r.Kind, r.Month, r.Day, r.EasterOffset, r.Weekday, r.Occurrence, r.FromYear, r.ToYear);
                    if (!r.IsActive)
                        rule.Deactivate();
                    calendar.Rules.Add(rule);
                }
                await _context.HolidayCalendars.AddAsync(calendar, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            });
        }

        var regimes = (await _context.ComplianceSettings.AsNoTracking().Select(c => c.CountryCode).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var complianceData in settings.Compliance ?? [])
        {
            await Try($"E-invoicing settings {complianceData.CountryCode}", async () =>
            {
                var compliance = new ComplianceSettings(complianceData.CountryCode);
                if (!regimes.Add(compliance.CountryCode))
                    return false;
                var address = complianceData.Address is { } a
                    ? new Address(a.Street, a.HouseNumber, a.City, a.ZipCode, a.Country, a.State)
                    : null;
                // Turned on only where this server offers the country's rules
                var enabled = complianceData.IsEnabled && _compliance.Find(compliance.CountryCode) is not null;
                compliance.Update(enabled, complianceData.LegalName, complianceData.TaxId, address, complianceData.Values);
                await _context.ComplianceSettings.AddAsync(compliance, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            });
        }

        _logger.LogInformation("Settings import complete: {Imported} imported, {Skipped} skipped, {Errors} errors",
            imported, skipped, errors.Count);

        return new ImportResultDto(imported, skipped, errors);
    }

    private async Task<bool> ImportNumberingAsync(InvoiceNumberingExportDto numbering, CancellationToken cancellationToken)
    {
        var sequence = await _context.InvoiceSequences.FirstOrDefaultAsync(cancellationToken);
        if (sequence is null)
        {
            sequence = new InvoiceSequence(Math.Max(1, numbering.NextNumber));
            sequence.SetNumberFormat(numbering.NumberFormat);
            await _context.InvoiceSequences.AddAsync(sequence, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        var changed = false;
        if (sequence.NumberFormat is null && !string.IsNullOrWhiteSpace(numbering.NumberFormat))
        {
            sequence.SetNumberFormat(numbering.NumberFormat);
            changed = true;
        }
        if (numbering.NextNumber > sequence.CurrentValue)
        {
            sequence.SetValue(numbering.NextNumber);
            changed = true;
        }
        if (changed)
            await _context.SaveChangesAsync(cancellationToken);
        return changed;
    }
}
