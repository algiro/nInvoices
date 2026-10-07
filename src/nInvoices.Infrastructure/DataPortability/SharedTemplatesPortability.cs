using Microsoft.EntityFrameworkCore;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Infrastructure.Data;

namespace nInvoices.Infrastructure.DataPortability;

/// <summary>
/// The templates that belong to no customer (invoice, monthly report and email), exported on their own.
/// </summary>
internal sealed class SharedTemplatesPortability
{
    private readonly ApplicationDbContext _context;

    public SharedTemplatesPortability(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SharedTemplatesExportDto> ExportAsync(CancellationToken cancellationToken)
    {
        var sharedInvoiceTemplates = await _context.InvoiceTemplates
            .Where(t => t.CustomerId == null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var sharedReportTemplates = await _context.MonthlyReportTemplates
            .Where(t => t.CustomerId == null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var sharedEmailTemplates = await _context.EmailTemplates
            .Where(t => t.CustomerId == null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new SharedTemplatesExportDto(
            sharedInvoiceTemplates.Select(t => new InvoiceTemplateExportDto(t.InvoiceType, t.Name, t.Content, t.IsActive, t.CreatedAt)).ToList(),
            sharedReportTemplates.Select(mt => new MonthlyReportTemplateExportDto(mt.InvoiceType, mt.Name, mt.Content, mt.IsActive, mt.CreatedAt)).ToList(),
            sharedEmailTemplates.Select(et => new EmailTemplateExportDto(et.Name, et.Subject, et.Body, et.IsActive, et.CreatedAt)).ToList());
    }

    /// <summary>
    /// Adds the shared templates that are not there yet (same kind, type and name); an imported
    /// template is only activated when the user has no active shared one of its kind and type.
    /// </summary>
    public async Task<(int Imported, int Skipped)> ImportAsync(
        SharedTemplatesExportDto shared,
        CancellationToken cancellationToken)
    {
        var imported = 0;
        var skipped = 0;

        var invoiceTemplates = await _context.InvoiceTemplates.Where(t => t.CustomerId == null).ToListAsync(cancellationToken);
        foreach (var data in shared.InvoiceTemplates)
        {
            if (invoiceTemplates.Any(t => t.InvoiceType == data.InvoiceType && t.Name == data.Name))
            {
                skipped++;
                continue;
            }

            var template = new InvoiceTemplate(null, data.InvoiceType, data.Name, data.Content);
            if (data.IsActive && !invoiceTemplates.Any(t => t.InvoiceType == data.InvoiceType && t.IsActive))
                template.Activate();
            invoiceTemplates.Add(template);
            await _context.InvoiceTemplates.AddAsync(template, cancellationToken);
            imported++;
        }

        var reportTemplates = await _context.MonthlyReportTemplates.Where(t => t.CustomerId == null).ToListAsync(cancellationToken);
        foreach (var data in shared.MonthlyReportTemplates)
        {
            if (reportTemplates.Any(t => t.Name == data.Name))
            {
                skipped++;
                continue;
            }

            var template = new MonthlyReportTemplate(null, data.Name, data.Content, data.InvoiceType);
            if (data.IsActive && !reportTemplates.Any(t => t.IsActive))
                template.Activate();
            reportTemplates.Add(template);
            await _context.MonthlyReportTemplates.AddAsync(template, cancellationToken);
            imported++;
        }

        var emailTemplates = await _context.EmailTemplates.Where(t => t.CustomerId == null).ToListAsync(cancellationToken);
        foreach (var data in shared.EmailTemplates)
        {
            if (emailTemplates.Any(t => t.Name == data.Name))
            {
                skipped++;
                continue;
            }

            var template = new EmailTemplate(null, data.Name, data.Subject, data.Body);
            if (data.IsActive && !emailTemplates.Any(t => t.IsActive))
                template.Activate();
            emailTemplates.Add(template);
            await _context.EmailTemplates.AddAsync(template, cancellationToken);
            imported++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (imported, skipped);
    }
}
