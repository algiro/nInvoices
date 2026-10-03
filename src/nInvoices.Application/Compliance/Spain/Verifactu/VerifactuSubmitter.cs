using System.Xml.Linq;
using nInvoices.Core.Compliance;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using Microsoft.Extensions.Options;
using nInvoices.Core.Configuration;

namespace nInvoices.Application.Compliance.Spain.Verifactu;

/// <summary>What one round of sending came to.</summary>
/// <param name="Sent">Records included in the request (0 when nothing was sent).</param>
/// <param name="Problem">Why nothing (or not everything) could be sent, if so; null otherwise.</param>
public sealed record SubmissionRun(int Sent, int Accepted, int AcceptedWithErrors, int Rejected, string? Problem);

/// <summary>Where the user records stand with the Tax Agency.</summary>
public sealed record SubmissionSummary(int Pending, int Accepted, int AcceptedWithErrors, int Rejected, string? FirstProblem);

/// <summary>
/// Sends the Verifactu records to the Tax Agency: in chain order, in batches, authenticated with the
/// user certificate, and keeping to the pause the Tax Agency asks for between submissions. A record the
/// Tax Agency rejects stops the ones after it, which depend on it.
/// </summary>
public interface IVerifactuSubmitter
{
    /// <summary>Sends what is waiting (and due) for the current user.</summary>
    Task<SubmissionRun> SubmitPendingAsync(CancellationToken cancellationToken = default);

    Task<SubmissionSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public sealed class VerifactuSubmitter : IVerifactuSubmitter
{
    public const int BatchSize = 100;
    private const int DefaultWaitSeconds = 60;

    private readonly IRepository<VerifactuSubmission> _submissions;
    private readonly IVerifactuRecordRepository _records;
    private readonly IRepository<ComplianceSettings> _settings;
    private readonly SigningCertificateLoader _certificates;
    private readonly IAeatVerifactuClient _client;
    private readonly IUnitOfWork _unitOfWork;
    private readonly VerifactuOptions _options;
    private readonly TimeProvider _time;

    public VerifactuSubmitter(
        IRepository<VerifactuSubmission> submissions,
        IVerifactuRecordRepository records,
        IRepository<ComplianceSettings> settings,
        SigningCertificateLoader certificates,
        IAeatVerifactuClient client,
        IUnitOfWork unitOfWork,
        IOptions<VerifactuOptions> options,
        TimeProvider time)
    {
        _submissions = submissions;
        _records = records;
        _settings = settings;
        _certificates = certificates;
        _client = client;
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _time = time;
    }

    public async Task<SubmissionRun> SubmitPendingAsync(CancellationToken cancellationToken = default)
    {
        var all = (await _submissions.GetAllAsync(cancellationToken)).OrderBy(s => s.Sequence).ToList();
        var pending = all.Where(s => s.Status == VerifactuSubmissionStatus.Pending).ToList();
        if (pending.Count == 0)
            return new SubmissionRun(0, 0, 0, 0, null);

        var now = _time.GetUtcNow().UtcDateTime;

        // The records after a rejected one chain to something the Tax Agency does not have
        var rejected = all.FirstOrDefault(s => s.Status == VerifactuSubmissionStatus.Rejected && s.Sequence < pending[0].Sequence);
        if (rejected is not null)
            return new SubmissionRun(0, 0, 0, 0,
                $"Record {rejected.Sequence} was rejected by the Tax Agency ({rejected.Message}); the records after it cannot be sent until that is dealt with");

        if (pending[0].NextAttemptAt > now)
            return new SubmissionRun(0, 0, 0, 0, null); // the Tax Agency asked for a pause

        var missing = _options.Missing();
        if (missing.Count > 0)
            return new SubmissionRun(0, 0, 0, 0, $"Verifactu is not set up on this server ({string.Join(", ", missing)} missing)");

        var settings = (await _settings.GetAllAsync(cancellationToken))
            .FirstOrDefault(s => s.CountryCode == SpainComplianceModule.CountryCodeValue);
        if (settings is null)
            return new SubmissionRun(0, 0, 0, 0, "Spain is not set up");

        var issues = new List<ComplianceIssue>();
        using var certificate = _certificates.Load(settings, issues);
        if (certificate is null)
            return new SubmissionRun(0, 0, 0, 0, issues[0].Message);

        var batch = pending.Take(BatchSize).ToList();
        var ids = batch.Select(b => b.RecordId).ToHashSet();
        var records = (await _records.FindAsync(r => ids.Contains(r.Id), cancellationToken)).ToDictionary(r => r.Id);

        var issuerTaxId = SpanishTaxId.Normalize(settings.TaxId);
        var xml = VerifactuXml.Batch(
            settings.LegalName ?? "", issuerTaxId,
            batch.OrderBy(b => b.Sequence).Select(b => XElement.Parse(records[b.RecordId].Xml)));

        var target = new AeatTarget(
            _options.IsProduction,
            settings.Values.GetValueOrDefault(SpainComplianceModule.SealCertificateKey) == "true");

        AeatResponse response;
        try
        {
            response = await _client.SendAsync(target, certificate, xml, cancellationToken);
        }
        catch (AeatException ex)
        {
            foreach (var submission in batch)
                submission.Failed(ex.Message, now, Backoff(submission.Attempts + 1));

            HoldBack(pending, now + Backoff(batch[0].Attempts));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new SubmissionRun(batch.Count, 0, 0, 0, ex.Message);
        }

        return await Apply(batch, pending, records, response, now, cancellationToken);
    }

    public async Task<SubmissionSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var all = (await _submissions.GetAllAsync(cancellationToken)).OrderBy(s => s.Sequence).ToList();
        var problem = all.FirstOrDefault(s => s.Status is VerifactuSubmissionStatus.Rejected or VerifactuSubmissionStatus.AcceptedWithErrors);
        var failing = all.FirstOrDefault(s => s.Status == VerifactuSubmissionStatus.Pending && s.Attempts > 0);

        return new SubmissionSummary(
            all.Count(s => s.Status == VerifactuSubmissionStatus.Pending),
            all.Count(s => s.Status == VerifactuSubmissionStatus.Accepted),
            all.Count(s => s.Status == VerifactuSubmissionStatus.AcceptedWithErrors),
            all.Count(s => s.Status == VerifactuSubmissionStatus.Rejected),
            problem is not null ? $"Record {problem.Sequence}: {problem.Message}"
                : failing is not null ? $"Record {failing.Sequence} could not be sent: {failing.Message}"
                : null);
    }

    private async Task<SubmissionRun> Apply(
        List<VerifactuSubmission> batch, List<VerifactuSubmission> pending, Dictionary<long, VerifactuRecord> records,
        AeatResponse response, DateTime now, CancellationToken cancellationToken)
    {
        int accepted = 0, withErrors = 0, rejected = 0;
        var wait = TimeSpan.FromSeconds(response.WaitSeconds > 0 ? response.WaitSeconds : DefaultWaitSeconds);

        // No line at all: the whole submission was refused (say, the header)
        if (response.Lines.Count == 0)
        {
            var reason = $"The Tax Agency did not accept the submission ({response.EnvelopeStatus})";
            foreach (var submission in batch)
                submission.Failed(reason, now, wait);

            HoldBack(pending, now + wait);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new SubmissionRun(batch.Count, 0, 0, 0, reason);
        }

        foreach (var submission in batch)
        {
            var record = records[submission.RecordId];
            var operation = record.Kind == VerifactuRecordKind.Issued ? "Alta" : "Anulacion";
            var line = response.Lines.FirstOrDefault(l => l.InvoiceNumber == record.InvoiceNumber && l.Operation == operation);
            if (line is null)
            {
                submission.Failed("The Tax Agency gave no answer for this record", now, wait);
                continue;
            }

            switch (line.Status)
            {
                case "Correcto":
                    submission.Answered(VerifactuSubmissionStatus.Accepted, response.Csv, null, null, now);
                    accepted++;
                    break;

                case "AceptadoConErrores":
                    submission.Answered(VerifactuSubmissionStatus.AcceptedWithErrors, response.Csv, line.ErrorCode, line.ErrorDescription, now);
                    withErrors++;
                    break;

                // 3000: the record is already registered (an earlier answer was lost): nothing is wrong
                case "Incorrecto" when line.ErrorCode == "3000":
                    submission.Answered(VerifactuSubmissionStatus.Accepted, response.Csv, line.ErrorCode, "The Tax Agency already had this record", now);
                    accepted++;
                    break;

                default:
                    submission.Answered(VerifactuSubmissionStatus.Rejected, response.Csv, line.ErrorCode, line.ErrorDescription, now);
                    rejected++;
                    break;
            }
        }

        // Everything still waiting (what was not answered, and what did not fit the batch) keeps to the
        // pause the Tax Agency asked for
        HoldBack(pending, now + wait);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SubmissionRun(batch.Count, accepted, withErrors, rejected, null);
    }

    /// <summary>Nothing still pending is sent before <paramref name="time"/> (but nothing is sent earlier than it already was).</summary>
    private static void HoldBack(IEnumerable<VerifactuSubmission> pending, DateTime time)
    {
        foreach (var submission in pending.Where(s => s.Status == VerifactuSubmissionStatus.Pending && s.NextAttemptAt < time))
            submission.WaitUntil(time);
    }

    /// <summary>1 minute after the first failure, then doubling, never more than an hour.</summary>
    private static TimeSpan Backoff(int attempt) => TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Max(0, attempt - 1))));
}
