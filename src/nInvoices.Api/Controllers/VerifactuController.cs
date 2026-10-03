using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Verifactu;

namespace nInvoices.Api.Controllers;

/// <summary>Verifactu, the Spanish tamper-evident record of every invoice issued.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class VerifactuController : ControllerBase
{
    private readonly IMediator _mediator;

    public VerifactuController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Where an invoice stands in the chain (its record, hash and QR code).</summary>
    [HttpGet("invoices/{invoiceId}")]
    [ProducesResponseType(typeof(InvoiceVerifactuDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceVerifactuDto>> GetInvoice(long invoiceId, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetInvoiceVerifactuQuery(invoiceId), cancellationToken));
    }

    /// <summary>How many records are waiting, accepted or rejected by the Tax Agency.</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(VerifactuStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VerifactuStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetVerifactuStatusQuery(), cancellationToken));
    }

    /// <summary>
    /// Sends the waiting records to the Tax Agency now, instead of waiting for the next automatic round.
    /// The pause the Tax Agency asks for between submissions still applies.
    /// </summary>
    [HttpPost("submit")]
    [ProducesResponseType(typeof(VerifactuSubmissionRunDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VerifactuSubmissionRunDto>> Submit(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new SubmitVerifactuRecordsCommand(), cancellationToken));
    }

    /// <summary>
    /// Checks the whole chain: numbering without gaps, each record pointing at the one before, every hash
    /// matching its contents. Anything changed or removed after the fact shows up as a problem.
    /// </summary>
    [HttpGet("chain/verify")]
    [ProducesResponseType(typeof(ChainReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChainReportDto>> VerifyChain(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new VerifyVerifactuChainQuery(), cancellationToken));
    }
}
