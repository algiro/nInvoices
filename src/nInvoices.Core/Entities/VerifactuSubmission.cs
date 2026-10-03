namespace nInvoices.Core.Entities;

public enum VerifactuSubmissionStatus
{
    /// <summary>Not sent yet, or sent without an answer: it will be tried again.</summary>
    Pending,

    /// <summary>The Tax Agency registered the record.</summary>
    Accepted,

    /// <summary>Registered, with remarks the Tax Agency raised (see the message).</summary>
    AcceptedWithErrors,

    /// <summary>The Tax Agency refused the record (see the message). The chain stops here until it is dealt with.</summary>
    Rejected
}

/// <summary>
/// Where one Verifactu record stands with the Tax Agency. Unlike the record, which is fixed for good,
/// this changes as the record is sent and answered.
/// </summary>
public sealed class VerifactuSubmission : OwnedEntityBase
{
    public long RecordId { get; private set; }

    /// <summary>Position of the record in the chain; records are sent in this order.</summary>
    public long Sequence { get; private set; }

    public VerifactuSubmissionStatus Status { get; private set; }

    /// <summary>How many times sending was tried and did not get an answer.</summary>
    public int Attempts { get; private set; }

    public DateTime? LastAttemptAt { get; private set; }

    /// <summary>Not before this time: the Tax Agency asks for a pause between submissions, and a failed one backs off.</summary>
    public DateTime NextAttemptAt { get; private set; }

    public DateTime? AnsweredAt { get; private set; }

    /// <summary>The secure verification code the Tax Agency gave the submission (CSV).</summary>
    public string? Csv { get; private set; }

    /// <summary>The Tax Agency error code for a record accepted with errors or rejected.</summary>
    public string? ErrorCode { get; private set; }

    /// <summary>What the Tax Agency said, or why the last attempt failed.</summary>
    public string? Message { get; private set; }

    public VerifactuRecord Record { get; set; } = null!;

    private VerifactuSubmission() { }

    public VerifactuSubmission(long recordId, long sequence, DateTime now)
    {
        RecordId = recordId;
        Sequence = sequence;
        Status = VerifactuSubmissionStatus.Pending;
        NextAttemptAt = now;
        CreatedAt = now;
    }

    /// <summary>For a record that was just created: the record id is only known once it is saved.</summary>
    public VerifactuSubmission(VerifactuRecord record, DateTime now) : this(record.Id, record.Sequence, now)
    {
        Record = record;
    }

    public void Answered(VerifactuSubmissionStatus status, string? csv, string? errorCode, string? message, DateTime now)
    {
        Status = status;
        Csv = csv;
        ErrorCode = errorCode;
        Message = message;
        LastAttemptAt = now;
        AnsweredAt = now;
        UpdatedAt = now;
    }

    /// <summary>The attempt got no usable answer (network, certificate, a fault): try again later.</summary>
    public void Failed(string message, DateTime now, TimeSpan retryAfter)
    {
        Attempts++;
        Message = message;
        LastAttemptAt = now;
        NextAttemptAt = now + retryAfter;
        UpdatedAt = now;
    }

    /// <summary>The Tax Agency asked for a pause before the next submission.</summary>
    public void WaitUntil(DateTime time)
    {
        NextAttemptAt = time;
        UpdatedAt = DateTime.UtcNow;
    }
}
