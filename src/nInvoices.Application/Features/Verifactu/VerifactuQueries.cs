using MediatR;
using nInvoices.Application.Compliance.Spain.Verifactu;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Verifactu;

/// <summary>Where an invoice stands in the Verifactu chain; not recorded when the user does not use Verifactu.</summary>
public sealed record GetInvoiceVerifactuQuery(long InvoiceId) : IRequest<InvoiceVerifactuDto>;

/// <summary>Checks the user Verifactu chain from end to end.</summary>
public sealed record VerifyVerifactuChainQuery : IRequest<ChainReportDto>;

public sealed class GetInvoiceVerifactuQueryHandler : IRequestHandler<GetInvoiceVerifactuQuery, InvoiceVerifactuDto>
{
    private readonly IVerifactuService _verifactu;
    private readonly IRepository<VerifactuSubmission> _submissions;

    public GetInvoiceVerifactuQueryHandler(IVerifactuService verifactu, IRepository<VerifactuSubmission> submissions)
    {
        _verifactu = verifactu;
        _submissions = submissions;
    }

    public async Task<InvoiceVerifactuDto> Handle(GetInvoiceVerifactuQuery request, CancellationToken cancellationToken)
    {
        var records = await _verifactu.GetRecordsAsync(request.InvoiceId, cancellationToken);
        var issued = records.FirstOrDefault(r => r.Kind == VerifactuRecordKind.Issued);
        if (issued is null)
            return new InvoiceVerifactuDto(false, null, null, null, null, null, VerifactuQr.Legend, VerifactuQr.Heading, false);

        var url = _verifactu.QrUrl(issued);
        var submission = (await _submissions.FindAsync(s => s.RecordId == issued.Id, cancellationToken)).FirstOrDefault();
        return new InvoiceVerifactuDto(
            true,
            issued.Sequence,
            issued.Hash,
            issued.GeneratedAt,
            url,
            VerifactuQr.Svg(url),
            VerifactuQr.Legend,
            VerifactuQr.Heading,
            records.Any(r => r.Kind == VerifactuRecordKind.Cancelled),
            submission?.Status.ToString(),
            submission?.Message,
            submission?.Csv);
    }
}

public sealed class VerifyVerifactuChainQueryHandler : IRequestHandler<VerifyVerifactuChainQuery, ChainReportDto>
{
    private readonly IVerifactuService _verifactu;

    public VerifyVerifactuChainQueryHandler(IVerifactuService verifactu)
    {
        _verifactu = verifactu;
    }

    public async Task<ChainReportDto> Handle(VerifyVerifactuChainQuery request, CancellationToken cancellationToken)
    {
        var report = await _verifactu.VerifyChainAsync(cancellationToken);
        return new ChainReportDto(
            report.Records,
            report.IsIntact,
            report.Problems.Select(p => new ChainProblemDto(p.Sequence, p.Message)).ToList());
    }
}

/// <summary>Where the user records stand with the Tax Agency.</summary>
public sealed record GetVerifactuStatusQuery : IRequest<VerifactuStatusDto>;

/// <summary>Sends the records that are waiting, now (the Tax Agency pause between submissions still applies).</summary>
public sealed record SubmitVerifactuRecordsCommand : IRequest<VerifactuSubmissionRunDto>;

public sealed class GetVerifactuStatusQueryHandler : IRequestHandler<GetVerifactuStatusQuery, VerifactuStatusDto>
{
    private readonly IVerifactuSubmitter _submitter;

    public GetVerifactuStatusQueryHandler(IVerifactuSubmitter submitter)
    {
        _submitter = submitter;
    }

    public async Task<VerifactuStatusDto> Handle(GetVerifactuStatusQuery request, CancellationToken cancellationToken)
    {
        var summary = await _submitter.GetSummaryAsync(cancellationToken);
        return new VerifactuStatusDto(summary.Pending, summary.Accepted, summary.AcceptedWithErrors, summary.Rejected, summary.FirstProblem);
    }
}

public sealed class SubmitVerifactuRecordsCommandHandler : IRequestHandler<SubmitVerifactuRecordsCommand, VerifactuSubmissionRunDto>
{
    private readonly IVerifactuSubmitter _submitter;

    public SubmitVerifactuRecordsCommandHandler(IVerifactuSubmitter submitter)
    {
        _submitter = submitter;
    }

    public async Task<VerifactuSubmissionRunDto> Handle(SubmitVerifactuRecordsCommand request, CancellationToken cancellationToken)
    {
        var run = await _submitter.SubmitPendingAsync(cancellationToken);
        return new VerifactuSubmissionRunDto(run.Sent, run.Accepted, run.AcceptedWithErrors, run.Rejected, run.Problem);
    }
}
