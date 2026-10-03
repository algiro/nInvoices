using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Compliance.EInvoice;

/// <summary>An e-invoice format that applies to the user, and the file generated in it for an invoice (if any).</summary>
public sealed record EInvoiceStatus(
    ICountryComplianceModule Module,
    IEInvoiceFormat Format,
    bool IsMandatory,
    InvoiceEInvoice? Stored);

/// <summary>What generating one format for an invoice came to.</summary>
public sealed record EInvoiceResult(IEInvoiceFormat Format, bool Generated, IReadOnlyList<ComplianceIssue> Issues);

/// <summary>
/// Generates and keeps the structured e-invoices (Facturae...) of the countries the user turned on.
/// With no country on, there are no formats and nothing happens.
/// </summary>
public interface IEInvoiceService
{
    /// <summary>The formats that apply to the invoice, each with its stored file if one was generated.</summary>
    /// <exception cref="KeyNotFoundException">The invoice does not exist.</exception>
    Task<IReadOnlyList<EInvoiceStatus>> GetStatusAsync(long invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates the invoice in each format that applies, replacing the stored file. A format
    /// whose rules the invoice does not meet is reported (with the reasons), not thrown.
    /// </summary>
    /// <param name="onlyMandatory">Only the formats the rules require for this customer.</param>
    /// <exception cref="KeyNotFoundException">The invoice does not exist.</exception>
    /// <exception cref="InvalidOperationException">The invoice is still a draft or is cancelled.</exception>
    Task<IReadOnlyList<EInvoiceResult>> GenerateAsync(long invoiceId, bool onlyMandatory, CancellationToken cancellationToken = default);
}

public sealed class EInvoiceService : IEInvoiceService
{
    private readonly IComplianceGate _gate;
    private readonly IEnumerable<IEInvoiceFormat> _formats;
    private readonly IInvoiceRepository _invoices;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<InvoiceEInvoice> _stored;
    private readonly IEInvoiceDocumentFactory _documents;
    private readonly ISecretProtector _protector;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public EInvoiceService(
        IComplianceGate gate,
        IEnumerable<IEInvoiceFormat> formats,
        IInvoiceRepository invoices,
        IRepository<Customer> customers,
        IRepository<InvoiceEInvoice> stored,
        IEInvoiceDocumentFactory documents,
        ISecretProtector protector,
        IUnitOfWork unitOfWork,
        TimeProvider time)
    {
        _gate = gate;
        _formats = formats;
        _invoices = invoices;
        _customers = customers;
        _stored = stored;
        _documents = documents;
        _protector = protector;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<IReadOnlyList<EInvoiceStatus>> GetStatusAsync(long invoiceId, CancellationToken cancellationToken = default)
    {
        var applicable = await ApplicableAsync(cancellationToken);
        if (applicable.Count == 0)
            return [];

        var invoice = await _invoices.GetByIdAsync(invoiceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice {invoiceId} not found");
        var customer = await _customers.GetByIdAsync(invoice.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer {invoice.CustomerId} not found");
        var stored = (await _stored.FindAsync(e => e.InvoiceId == invoiceId, cancellationToken)).ToList();

        return applicable
            .Select(a =>
            {
                var buyer = new EInvoiceParty(customer.Name, customer.FiscalId, customer.Address, customer.GetComplianceValues(a.Active.Module.CountryCode));
                return new EInvoiceStatus(
                    a.Active.Module,
                    a.Format,
                    a.Format.IsMandatoryFor(buyer),
                    stored.FirstOrDefault(e => e.FormatId == a.Format.FormatId));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<EInvoiceResult>> GenerateAsync(long invoiceId, bool onlyMandatory, CancellationToken cancellationToken = default)
    {
        var applicable = await ApplicableAsync(cancellationToken);
        if (applicable.Count == 0)
            return [];

        var invoice = await _invoices.GetByIdAsync(invoiceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice {invoiceId} not found");
        if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
            throw new InvalidOperationException(invoice.Status == InvoiceStatus.Draft
                ? "Finalize the invoice before generating its e-invoice"
                : "A cancelled invoice has no e-invoice");

        var existing = (await _stored.FindAsync(e => e.InvoiceId == invoiceId, cancellationToken)).ToList();
        var results = new List<EInvoiceResult>();

        foreach (var (active, format) in applicable)
        {
            var document = await _documents.CreateAsync(invoiceId, active.Settings, cancellationToken);
            if (onlyMandatory && !format.IsMandatoryFor(document.Buyer))
                continue;

            var issues = format.Validate(document).ToList();

            X509Certificate2? certificate = null;
            if (format.RequiresSignature)
                certificate = LoadCertificate(active.Settings, issues);

            if (issues.Count > 0)
            {
                results.Add(new EInvoiceResult(format, false, issues));
                continue;
            }

            using (certificate)
            {
                var artifact = format.Build(document, certificate);

                var current = existing.FirstOrDefault(e => e.FormatId == format.FormatId);
                if (current is null)
                    await _stored.AddAsync(new InvoiceEInvoice(invoiceId, format.CountryCode, format.FormatId, artifact.Content, artifact.ContentType, artifact.FileExtension), cancellationToken);
                else
                {
                    current.Replace(artifact.Content, artifact.ContentType, artifact.FileExtension);
                    await _stored.UpdateAsync(current, cancellationToken);
                }
            }

            results.Add(new EInvoiceResult(format, true, []));
        }

        if (results.Any(r => r.Generated))
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return results;
    }

    /// <summary>The formats of the countries the user turned on, with those settings.</summary>
    private async Task<List<(ActiveCompliance Active, IEInvoiceFormat Format)>> ApplicableAsync(CancellationToken cancellationToken)
    {
        var active = await _gate.GetActiveAsync(cancellationToken);
        return active
            .SelectMany(a => _formats
                .Where(f => string.Equals(f.CountryCode, a.Module.CountryCode, StringComparison.OrdinalIgnoreCase))
                .Select(f => (a, f)))
            .ToList();
    }

    /// <summary>The user's signing certificate; on a problem, null and the reason added to <paramref name="issues"/>.</summary>
    private X509Certificate2? LoadCertificate(ComplianceSettings settings, List<ComplianceIssue> issues)
    {
        if (!settings.HasCertificate)
        {
            issues.Add(new ComplianceIssue(null, "Upload your signing certificate in Settings: Facturae invoices must be signed"));
            return null;
        }

        if (settings.CertificateNotAfter is { } notAfter && notAfter < _time.GetUtcNow().UtcDateTime)
        {
            issues.Add(new ComplianceIssue(null, $"The signing certificate expired on {notAfter:yyyy-MM-dd}"));
            return null;
        }

        try
        {
            var pfx = Convert.FromBase64String(_protector.Unprotect(settings.ProtectedCertificate!));
            return X509CertificateLoader.LoadPkcs12(pfx, _protector.Unprotect(settings.ProtectedCertificatePassword!));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            issues.Add(new ComplianceIssue(null, "The stored signing certificate can no longer be read: upload it again"));
            return null;
        }
    }
}
