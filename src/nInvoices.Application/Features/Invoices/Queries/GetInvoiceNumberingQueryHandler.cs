using MediatR;
using Microsoft.Extensions.Options;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Queries;

public sealed class GetInvoiceNumberingQueryHandler : IRequestHandler<GetInvoiceNumberingQuery, InvoiceNumberingDto>
{
    private readonly IRepository<InvoiceSequence> _sequences;
    private readonly InvoiceSettings _settings;

    public GetInvoiceNumberingQueryHandler(IRepository<InvoiceSequence> sequences, IOptions<InvoiceSettings> settings)
    {
        _sequences = sequences;
        _settings = settings.Value;
    }

    public async Task<InvoiceNumberingDto> Handle(GetInvoiceNumberingQuery request, CancellationToken cancellationToken)
    {
        // The query filter returns only the current user's row
        var sequence = (await _sequences.GetAllAsync(cancellationToken)).FirstOrDefault();

        return InvoiceNumberingMapper.ToDto(sequence, _settings.NumberFormat);
    }
}
