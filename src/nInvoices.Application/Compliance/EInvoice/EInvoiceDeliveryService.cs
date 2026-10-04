using nInvoices.Core.Compliance;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Compliance.EInvoice;

/// <summary>A delivery channel as it stands for one invoice.</summary>
/// <param name="Applies">The channel takes invoices to this customer.</param>
/// <param name="HasFile">The e-invoice for the channel's format has been generated.</param>
/// <param name="NotReady">What still has to be filled in before delivering.</param>
/// <param name="Submission">The delivery, if the invoice was sent through the channel.</param>
public sealed record ChannelStatus(
    IEInvoiceChannel Channel,
    ICountryComplianceModule Module,
    bool Applies,
    bool HasFile,
    IReadOnlyList<ComplianceIssue> NotReady,
    EInvoiceSubmission? Submission);

/// <summary>What sending or refreshing came to; <see cref="Message"/> is for the user when it failed.</summary>
public sealed record DeliveryResult(bool Succeeded, string? Message, EInvoiceSubmission? Submission);

/// <summary>
/// Delivers the e-invoice of an invoice through a channel, and keeps track of where it stands there.
/// Sending is always the user's own act (it goes to a public body and cannot be taken back), never automatic.
/// </summary>
public interface IEInvoiceDeliveryService
{
    /// <summary>The channels of the countries the user turned on, for this invoice. Empty if none.</summary>
    /// <exception cref="KeyNotFoundException">The invoice does not exist.</exception>
    Task<IReadOnlyList<ChannelStatus>> GetChannelsAsync(long invoiceId, CancellationToken cancellationToken = default);

    Task<DeliveryResult> SendAsync(long invoiceId, string channelId, CancellationToken cancellationToken = default);

    Task<DeliveryResult> RefreshAsync(long invoiceId, string channelId, CancellationToken cancellationToken = default);
}

public sealed class EInvoiceDeliveryService : IEInvoiceDeliveryService
{
    private readonly IComplianceGate _gate;
    private readonly IEnumerable<IEInvoiceChannel> _channels;
    private readonly IInvoiceRepository _invoices;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<InvoiceEInvoice> _files;
    private readonly IRepository<EInvoiceSubmission> _submissions;
    private readonly SigningCertificateLoader _certificates;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public EInvoiceDeliveryService(
        IComplianceGate gate,
        IEnumerable<IEInvoiceChannel> channels,
        IInvoiceRepository invoices,
        IRepository<Customer> customers,
        IRepository<InvoiceEInvoice> files,
        IRepository<EInvoiceSubmission> submissions,
        SigningCertificateLoader certificates,
        IUnitOfWork unitOfWork,
        TimeProvider time)
    {
        _gate = gate;
        _channels = channels;
        _invoices = invoices;
        _customers = customers;
        _files = files;
        _submissions = submissions;
        _certificates = certificates;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<IReadOnlyList<ChannelStatus>> GetChannelsAsync(long invoiceId, CancellationToken cancellationToken = default)
    {
        var active = await _gate.GetActiveAsync(cancellationToken);
        var applicable = active
            .SelectMany(a => _channels
                .Where(c => string.Equals(c.CountryCode, a.Module.CountryCode, StringComparison.OrdinalIgnoreCase))
                .Select(c => (Active: a, Channel: c)))
            .ToList();
        if (applicable.Count == 0)
            return [];

        var invoice = await _invoices.GetByIdAsync(invoiceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice {invoiceId} not found");
        var customer = await _customers.GetByIdAsync(invoice.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer {invoice.CustomerId} not found");
        var files = (await _files.FindAsync(f => f.InvoiceId == invoiceId, cancellationToken)).ToList();
        var submissions = (await _submissions.GetAllAsync(cancellationToken)).ToList();

        return applicable.Select(a =>
        {
            var buyer = Buyer(customer, a.Active.Module.CountryCode);
            var file = files.FirstOrDefault(f => f.FormatId == a.Channel.FormatId);
            return new ChannelStatus(
                a.Channel,
                a.Active.Module,
                a.Channel.AppliesTo(buyer),
                file is not null,
                a.Channel.CheckReady(a.Active.Settings.ToIssuerProfile()),
                file is null ? null : submissions.FirstOrDefault(s => s.InvoiceEInvoiceId == file.Id && s.ChannelId == a.Channel.ChannelId));
        }).ToList();
    }

    public async Task<DeliveryResult> SendAsync(long invoiceId, string channelId, CancellationToken cancellationToken = default)
    {
        var (channel, active, invoice, customer, problem) = await ResolveAsync(invoiceId, channelId, cancellationToken);
        if (problem is not null)
            return Fail(problem);

        if (invoice!.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
            return Fail(invoice.Status == InvoiceStatus.Draft ? "Finalize the invoice first" : "A cancelled invoice cannot be sent");

        var file = (await _files.FindAsync(f => f.InvoiceId == invoiceId && f.FormatId == channel!.FormatId, cancellationToken)).FirstOrDefault();
        if (file is null)
            return Fail("Generate the e-invoice first");

        var existing = (await _submissions.FindAsync(s => s.InvoiceEInvoiceId == file.Id && s.ChannelId == channel!.ChannelId, cancellationToken)).FirstOrDefault();
        if (existing is not null)
            return new DeliveryResult(false, $"It was already sent ({existing.Reference})", existing);

        if (!channel!.AppliesTo(Buyer(customer!, active!.Module.CountryCode)))
            return Fail($"Invoices to this customer do not go through {channel.DisplayName}");

        var notReady = channel.CheckReady(active.Settings.ToIssuerProfile());
        if (notReady.Count > 0)
            return Fail(string.Join("; ", notReady.Select(i => i.Message)));

        var issues = new List<ComplianceIssue>();
        using var certificate = _certificates.Load(active.Settings, issues);
        if (certificate is null)
            return Fail(issues[0].Message);

        ChannelDelivery delivery;
        try
        {
            delivery = await channel.SendAsync(
                new EInvoiceShipment(invoice.Number.ToString(), EInvoiceMapper.FileName(invoice.Number.ToString(), file), file.Content, active.Settings.ToIssuerProfile()),
                certificate, cancellationToken);
        }
        catch (ChannelException ex)
        {
            return Fail(ex.Message); // nothing is stored: it can be tried again
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var submission = new EInvoiceSubmission(file.Id, channel.ChannelId, channel.EnvironmentName, delivery.Reference, now);
        submission.Update(delivery.StatusCode, delivery.StatusName, delivery.CancellationStatus, delivery.RegisteredAt, now);
        await _submissions.AddAsync(submission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeliveryResult(true, null, submission);
    }

    public async Task<DeliveryResult> RefreshAsync(long invoiceId, string channelId, CancellationToken cancellationToken = default)
    {
        var (channel, active, _, _, problem) = await ResolveAsync(invoiceId, channelId, cancellationToken);
        if (problem is not null)
            return Fail(problem);

        var file = (await _files.FindAsync(f => f.InvoiceId == invoiceId && f.FormatId == channel!.FormatId, cancellationToken)).FirstOrDefault();
        var submission = file is null
            ? null
            : (await _submissions.FindAsync(s => s.InvoiceEInvoiceId == file.Id && s.ChannelId == channel!.ChannelId, cancellationToken)).FirstOrDefault();
        if (submission is null)
            return Fail("It has not been sent");

        var issues = new List<ComplianceIssue>();
        using var certificate = _certificates.Load(active!.Settings, issues);
        if (certificate is null)
            return new DeliveryResult(false, issues[0].Message, submission);

        var now = _time.GetUtcNow().UtcDateTime;
        try
        {
            var delivery = await channel!.RefreshAsync(submission.Reference, certificate, cancellationToken);
            submission.Update(delivery.StatusCode, delivery.StatusName, delivery.CancellationStatus, delivery.RegisteredAt, now);
            await _submissions.UpdateAsync(submission, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new DeliveryResult(true, null, submission);
        }
        catch (ChannelException ex)
        {
            submission.CheckFailed(ex.Message, now);
            await _submissions.UpdateAsync(submission, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new DeliveryResult(false, ex.Message, submission);
        }
    }

    private async Task<(IEInvoiceChannel? Channel, ActiveCompliance? Active, Invoice? Invoice, Customer? Customer, string? Problem)> ResolveAsync(
        long invoiceId, string channelId, CancellationToken cancellationToken)
    {
        var channel = _channels.FirstOrDefault(c => string.Equals(c.ChannelId, channelId, StringComparison.OrdinalIgnoreCase));
        if (channel is null)
            return (null, null, null, null, $"Unknown channel \"{channelId}\"");

        var active = (await _gate.GetActiveAsync(cancellationToken))
            .FirstOrDefault(a => string.Equals(a.Module.CountryCode, channel.CountryCode, StringComparison.OrdinalIgnoreCase));
        if (active is null)
            return (channel, null, null, null, $"{channel.DisplayName} is not turned on");

        if (channel.UnavailableReason is { } reason)
            return (channel, active, null, null, reason);

        var invoice = await _invoices.GetByIdAsync(invoiceId, cancellationToken);
        if (invoice is null)
            return (channel, active, null, null, $"Invoice {invoiceId} not found");

        var customer = await _customers.GetByIdAsync(invoice.CustomerId, cancellationToken);
        return (channel, active, invoice, customer, customer is null ? "The customer was not found" : null);
    }

    private static EInvoiceParty Buyer(Customer customer, string countryCode) =>
        new(customer.Name, customer.FiscalId, customer.Address, customer.GetComplianceValues(countryCode));

    private static DeliveryResult Fail(string message) => new(false, message, null);
}
