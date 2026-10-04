using System.Globalization;
using System.Reflection;
using nInvoices.Application.Services.Holidays;
using nInvoices.Core.Compliance;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace nInvoices.Application.Compliance.Spain.Verifactu;

/// <summary>An invoice cannot be recorded because it does not meet Verifactu rules; <see cref="Issues"/> says why.</summary>
public sealed class VerifactuException : InvalidOperationException
{
    public IReadOnlyList<ComplianceIssue> Issues { get; }

    public VerifactuException(IReadOnlyList<ComplianceIssue> issues)
        : base("Verifactu cannot record this invoice: " + string.Join("; ", issues.Select(i => i.Message)))
    {
        Issues = issues;
    }
}

/// <summary>
/// The user Verifactu chain: adds the record of every invoice issued or cancelled, keeps the chain
/// intact and checks it. Nothing happens for a user who has not turned Verifactu on.
/// </summary>
public interface IVerifactuService
{
    /// <summary>Whether the current user has Verifactu turned on (and the server offers it).</summary>
    Task<bool> IsActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the record of an invoice being issued to the chain (the caller saves it together with the
    /// invoice). Does nothing, and returns false, if Verifactu is off or the invoice already has its record.
    /// </summary>
    /// <exception cref="VerifactuException">The invoice does not meet the rules.</exception>
    Task<bool> RecordIssuedAsync(Invoice invoice, Customer customer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the record of an invoice being cancelled. Does nothing, and returns false, if the invoice was
    /// never recorded or its cancellation already is.
    /// </summary>
    Task<bool> RecordCancelledAsync(Invoice invoice, CancellationToken cancellationToken = default);

    /// <summary>An invoice with records is part of the chain and cannot be deleted; cancel it instead.</summary>
    /// <exception cref="InvalidOperationException">The invoice has records.</exception>
    Task EnsureCanDeleteAsync(Invoice invoice, CancellationToken cancellationToken = default);

    Task<ChainReport> VerifyChainAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VerifactuRecord>> GetRecordsAsync(long invoiceId, CancellationToken cancellationToken = default);

    /// <summary>The URL of the QR code of an issued invoice.</summary>
    string QrUrl(VerifactuRecord issued);
}

public sealed class VerifactuService : IVerifactuService
{
    private const string InvoiceType = "F1";

    private readonly IComplianceGate _gate;
    private readonly IVerifactuRecordRepository _records;
    private readonly IInvoiceRepository _invoices;
    private readonly IRepository<Tax> _taxes;
    private readonly IRepository<VerifactuSubmission> _submissions;
    private readonly VerifactuOptions _options;
    private readonly TimeProvider _time;

    // The last record of the chain, kept for the request so records added in it chain to each other
    private VerifactuRecord? _last;
    private bool _lastLoaded;

    public VerifactuService(
        IComplianceGate gate,
        IVerifactuRecordRepository records,
        IInvoiceRepository invoices,
        IRepository<Tax> taxes,
        IRepository<VerifactuSubmission> submissions,
        IOptions<VerifactuOptions> options,
        TimeProvider time)
    {
        _gate = gate;
        _records = records;
        _invoices = invoices;
        _taxes = taxes;
        _submissions = submissions;
        _options = options.Value;
        _time = time;
    }

    public async Task<bool> IsActiveAsync(CancellationToken cancellationToken = default) =>
        await ActiveSettingsAsync(cancellationToken) is not null;

    public async Task<bool> RecordIssuedAsync(Invoice invoice, Customer customer, CancellationToken cancellationToken = default)
    {
        var settings = await ActiveSettingsAsync(cancellationToken);
        if (settings is null)
            return false;

        if ((await _records.GetByInvoiceAsync(invoice.Id, cancellationToken)).Any(r => r.Kind == VerifactuRecordKind.Issued))
            return false;

        var issues = new List<ComplianceIssue>();
        var missing = _options.Missing();
        if (missing.Count > 0)
            throw new VerifactuException([new ComplianceIssue(null, $"Verifactu is not set up on this server ({string.Join(", ", missing)} missing)")]);

        // The tax lines come with the invoice
        var full = await _invoices.GetByIdWithRelatedAsync(invoice.Id, cancellationToken) ?? invoice;
        var taxes = (await _taxes.FindAsync(t => t.CustomerId == full.CustomerId, cancellationToken)).ToList();

        var data = Describe(full, customer, settings, taxes, issues);
        if (issues.Count > 0 || data is null)
            throw new VerifactuException(issues);

        var previous = await LastAsync(cancellationToken);
        var stamp = Stamp(previous);
        var totalTax = VerifactuHash.Amount(data.TotalTax);
        var totalAmount = VerifactuHash.Amount(data.TotalAmount);
        var date = VerifactuHash.Date(data.IssueDate);
        var previousHash = previous?.Hash ?? "";

        var hash = VerifactuHash.Issued(data.IssuerTaxId, data.InvoiceNumber, date, data.InvoiceType, totalTax, totalAmount, previousHash, stamp);
        var xml = VerifactuXml.Issued(data, BillingSystem(), Link(previous), stamp, hash);

        await AppendAsync(new VerifactuRecord(
            (previous?.Sequence ?? 0) + 1, VerifactuRecordKind.Issued, full.Id,
            data.IssuerTaxId, data.InvoiceNumber, date, data.InvoiceType, totalTax, totalAmount,
            previousHash, stamp, hash, xml.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)), cancellationToken);
        return true;
    }

    public async Task<bool> RecordCancelledAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        var settings = await ActiveSettingsAsync(cancellationToken);
        if (settings is null)
            return false;

        var existing = await _records.GetByInvoiceAsync(invoice.Id, cancellationToken);
        var issued = existing.FirstOrDefault(r => r.Kind == VerifactuRecordKind.Issued);
        if (issued is null || existing.Any(r => r.Kind == VerifactuRecordKind.Cancelled))
            return false;

        var previous = await LastAsync(cancellationToken);
        var stamp = Stamp(previous);
        var previousHash = previous?.Hash ?? "";
        var hash = VerifactuHash.Cancelled(issued.IssuerTaxId, issued.InvoiceNumber, issued.IssueDate, previousHash, stamp);
        var xml = VerifactuXml.Cancelled(
            issued.IssuerTaxId, issued.InvoiceNumber, DateOnly.ParseExact(issued.IssueDate, "dd-MM-yyyy", CultureInfo.InvariantCulture),
            BillingSystem(), Link(previous), stamp, hash);

        await AppendAsync(new VerifactuRecord(
            (previous?.Sequence ?? 0) + 1, VerifactuRecordKind.Cancelled, invoice.Id,
            issued.IssuerTaxId, issued.InvoiceNumber, issued.IssueDate, "", "", "",
            previousHash, stamp, hash, xml.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)), cancellationToken);
        return true;
    }

    public async Task EnsureCanDeleteAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if ((await _records.GetByInvoiceAsync(invoice.Id, cancellationToken)).Count > 0)
            throw new InvalidOperationException(
                $"Invoice {invoice.Number} was recorded for Verifactu and cannot be deleted: cancel it instead, which records the cancellation.");
    }

    public async Task<ChainReport> VerifyChainAsync(CancellationToken cancellationToken = default) =>
        VerifactuChainVerifier.Verify(await _records.GetChainAsync(cancellationToken));

    public async Task<IReadOnlyList<VerifactuRecord>> GetRecordsAsync(long invoiceId, CancellationToken cancellationToken = default) =>
        await _records.GetByInvoiceAsync(invoiceId, cancellationToken);

    public string QrUrl(VerifactuRecord issued) =>
        VerifactuQr.Url(_options.IsProduction, issued.IssuerTaxId, issued.InvoiceNumber, issued.IssueDate, issued.TotalAmount);

    // ---------------------------------------------------------------------------------------------

    /// <summary>The user settings for Spain, if the user turned Spain and Verifactu on.</summary>
    private async Task<Core.Entities.ComplianceSettings?> ActiveSettingsAsync(CancellationToken cancellationToken)
    {
        var active = await _gate.GetActiveAsync(cancellationToken);
        return active
            .FirstOrDefault(a => a.Module.CountryCode == SpainComplianceModule.CountryCodeValue
                && a.Settings.Values.GetValueOrDefault(SpainComplianceModule.VerifactuKey) == "true")
            ?.Settings;
    }

    private async Task<VerifactuRecord?> LastAsync(CancellationToken cancellationToken)
    {
        if (!_lastLoaded)
        {
            _last = await _records.GetLastAsync(cancellationToken);
            _lastLoaded = true;
        }

        return _last;
    }

    private async Task AppendAsync(VerifactuRecord record, CancellationToken cancellationToken)
    {
        await _records.AddAsync(record, cancellationToken);

        // The record is saved with its place in the queue for the Tax Agency
        await _submissions.AddAsync(new VerifactuSubmission(record, _time.GetUtcNow().UtcDateTime), cancellationToken);
        _last = record;
        _lastLoaded = true;
    }

    private static VerifactuPrevious? Link(VerifactuRecord? previous) =>
        previous is null ? null : new VerifactuPrevious(previous.IssuerTaxId, previous.InvoiceNumber, previous.IssueDate, previous.Hash);

    /// <summary>The time stamp of a new record: now, and never before the record it follows.</summary>
    private string Stamp(VerifactuRecord? previous)
    {
        var now = _time.GetUtcNow();
        if (previous is not null
            && DateTimeOffset.TryParse(previous.GeneratedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var last)
            && last > now)
            now = last;

        return SpanishClock.Stamp(now);
    }

    private VerifactuSystem BillingSystem() => new(
        _options.ProducerName!,
        _options.ProducerTaxId!,
        _options.SystemName,
        _options.SystemId,
        _options.Version ?? AssemblyVersion(),
        _options.InstallationNumber,
        _options.MultipleTaxpayers);

    private static string AssemblyVersion()
    {
        var version = typeof(VerifactuService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }

    /// <summary>What the record of the invoice says; null, with the reasons in <paramref name="issues"/>, if it cannot be recorded.</summary>
    private VerifactuInvoiceData? Describe(
        Invoice invoice, Customer customer, Core.Entities.ComplianceSettings settings, List<Tax> taxes, List<ComplianceIssue> issues)
    {
        var issuerTaxId = SpanishTaxId.Normalize(settings.TaxId);
        if (issuerTaxId.Length != 9)
            issues.Add(new ComplianceIssue(null, "Your NIF in the Spanish settings must have nine characters"));

        var number = invoice.Number.ToString();
        if (number.Length is 0 or > 60 || number.Any(c => c < 32 || c > 126))
            issues.Add(new ComplianceIssue(null, "The invoice number must be 1 to 60 printable ASCII characters"));

        var recipient = Recipient(customer, issues);

        var added = invoice.TaxLines.Where(t => t.Rate >= 0).OrderBy(t => t.Order).ToList();
        if (added.Count != 1)
            issues.Add(new ComplianceIssue(null, added.Count == 0
                ? "The invoice needs a VAT (or IGIC) line; for an exempt invoice add a tax at 0% and say why in the tax settings"
                : "Verifactu is supported with one VAT (or IGIC) line per invoice"));

        var taxableBase = invoice.Subtotal.Amount + invoice.TotalExpenses.Amount;
        var breakdown = new List<VerifactuBreakdown>();
        foreach (var line in added)
        {
            if (Math.Abs(line.BaseAmount.Amount - taxableBase) > 0.01m)
            {
                issues.Add(new ComplianceIssue(null, $"Tax \"{line.Description}\" is not calculated on the whole invoice, which Verifactu cannot express"));
                continue;
            }

            var values = taxes.FirstOrDefault(t => t.TaxId == line.TaxId)
                ?.GetComplianceValues(SpainComplianceModule.CountryCodeValue);
            var igic = values?.GetValueOrDefault(SpainComplianceModule.TaxTypeKey) == SpainComplianceModule.Igic;

            string operation;
            if (line.Rate > 0)
            {
                operation = "S1";
            }
            else
            {
                operation = values?.GetValueOrDefault(SpainComplianceModule.OperationKey) ?? "";
                if (operation.Length == 0)
                    issues.Add(new ComplianceIssue(null, $"Choose why tax \"{line.Description}\" is 0% (exempt, not subject...) in Taxes, under Verifactu"));
                else if (operation is "E7" or "E8" && !igic)
                    issues.Add(new ComplianceIssue(null, $"Tax \"{line.Description}\": the exemptions E7 and E8 exist for IGIC only; set the tax type to IGIC or choose another"));
            }

            breakdown.Add(new VerifactuBreakdown(
                operation, line.Rate, VerifactuHashRound(line.BaseAmount.Amount), operation == "S1" ? VerifactuHashRound(line.TaxAmount.Amount) : 0m, igic));
        }

        if (issues.Count > 0 || recipient is null)
            return null;

        var totalTax = breakdown.Sum(b => b.Tax);
        var description = invoice.Month is { } month && invoice.Year is { } year
            ? $"Servicios profesionales {month:00}/{year}"
            : "Servicios profesionales";

        return new VerifactuInvoiceData(
            issuerTaxId,
            settings.LegalName ?? "",
            number,
            invoice.IssueDate,
            InvoiceType,
            description,
            recipient,
            breakdown,
            totalTax,
            VerifactuHashRound(taxableBase) + totalTax);
    }

    private static decimal VerifactuHashRound(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static VerifactuRecipient? Recipient(Customer customer, List<ComplianceIssue> issues)
    {
        var country = CountryCodes.FromName(customer.Address.Country);
        if (country is null)
        {
            issues.Add(new ComplianceIssue(null, $"The country of the customer (\"{customer.Address.Country}\") is not recognized"));
            return null;
        }

        if (country == "ES")
        {
            var nif = SpanishTaxId.Normalize(customer.FiscalId);
            if (nif.Length != 9 || !SpanishTaxId.IsValid(nif))
            {
                issues.Add(new ComplianceIssue(null, "The customer's NIF is not a valid Spanish NIF, NIE or CIF"));
                return null;
            }

            return new VerifactuRecipient(customer.Name, nif, null, null, null);
        }

        var id = new string(customer.FiscalId.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (id.Length is 0 or > 20)
        {
            issues.Add(new ComplianceIssue(null, "The customer's tax id must have 1 to 20 characters"));
            return null;
        }

        // 02: a VAT number within the EU, 04: the id in the country of residence
        var type = FacturaeCountriesIsEu(country) ? "02" : "04";
        return new VerifactuRecipient(customer.Name, null, country, type, id);
    }

    private static bool FacturaeCountriesIsEu(string alpha2) =>
        Facturae.FacturaeCountries.ResidenceTypeCode(alpha2) == "U";
}
