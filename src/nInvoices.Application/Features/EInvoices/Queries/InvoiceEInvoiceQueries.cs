using Mediator;
using nInvoices.Application.Compliance.EInvoice;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Application.Exceptions;

namespace nInvoices.Application.Features.EInvoices.Queries;

/// <summary>The e-invoice formats that apply to an invoice (empty when the user has no country on).</summary>
public sealed record GetInvoiceEInvoicesQuery(long InvoiceId) : IRequest<IReadOnlyList<InvoiceEInvoiceDto>>;

/// <summary>The generated file of an invoice in a format; null if the invoice has none in that format.</summary>
public sealed record GetInvoiceEInvoiceFileQuery(long InvoiceId, string FormatId) : IRequest<EInvoiceFileDto?>;

public sealed class GetInvoiceEInvoicesQueryHandler : IRequestHandler<GetInvoiceEInvoicesQuery, IReadOnlyList<InvoiceEInvoiceDto>>
{
    private readonly IEInvoiceService _eInvoices;
    private readonly IRepository<Invoice> _invoices;

    public GetInvoiceEInvoicesQueryHandler(IEInvoiceService eInvoices, IRepository<Invoice> invoices)
    {
        _eInvoices = eInvoices;
        _invoices = invoices;
    }

    /// <exception cref="KeyNotFoundException">The invoice does not exist.</exception>
    public async ValueTask<IReadOnlyList<InvoiceEInvoiceDto>> Handle(GetInvoiceEInvoicesQuery request, CancellationToken cancellationToken)
    {
        var statuses = await _eInvoices.GetStatusAsync(request.InvoiceId, cancellationToken);
        if (statuses.Count == 0)
            return [];

        var invoice = await _invoices.GetByIdAsync(request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException($"Invoice {request.InvoiceId} not found");

        return statuses.Select(s => EInvoiceMapper.ToDto(s, invoice.Number.ToString())).ToList();
    }
}

public sealed class GetInvoiceEInvoiceFileQueryHandler : IRequestHandler<GetInvoiceEInvoiceFileQuery, EInvoiceFileDto?>
{
    private readonly IRepository<InvoiceEInvoice> _stored;
    private readonly IRepository<Invoice> _invoices;

    public GetInvoiceEInvoiceFileQueryHandler(IRepository<InvoiceEInvoice> stored, IRepository<Invoice> invoices)
    {
        _stored = stored;
        _invoices = invoices;
    }

    public async ValueTask<EInvoiceFileDto?> Handle(GetInvoiceEInvoiceFileQuery request, CancellationToken cancellationToken)
    {
        var file = (await _stored.FindAsync(e => e.InvoiceId == request.InvoiceId && e.FormatId == request.FormatId, cancellationToken))
            .FirstOrDefault();
        if (file is null)
            return null;

        var invoice = await _invoices.GetByIdAsync(request.InvoiceId, cancellationToken);
        return invoice is null
            ? null
            : new EInvoiceFileDto(file.Content, file.ContentType, EInvoiceMapper.FileName(invoice.Number.ToString(), file));
    }
}
