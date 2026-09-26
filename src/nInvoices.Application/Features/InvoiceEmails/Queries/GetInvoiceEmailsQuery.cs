using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceEmails.Queries;

/// <summary>The Gmail drafts created for an invoice, newest first.</summary>
public sealed record GetInvoiceEmailsQuery(long InvoiceId) : IRequest<IReadOnlyList<InvoiceEmailDto>>;

public sealed class GetInvoiceEmailsQueryHandler : IRequestHandler<GetInvoiceEmailsQuery, IReadOnlyList<InvoiceEmailDto>>
{
    private readonly IRepository<InvoiceEmail> _repository;

    public GetInvoiceEmailsQueryHandler(IRepository<InvoiceEmail> repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<InvoiceEmailDto>> Handle(GetInvoiceEmailsQuery request, CancellationToken cancellationToken)
    {
        var emails = await _repository.FindAsync(e => e.InvoiceId == request.InvoiceId, cancellationToken);
        return emails
            .OrderByDescending(e => e.CreatedAt)
            .Select(InvoiceEmailMapper.ToDto)
            .ToList();
    }
}
