using Mediator;
using nInvoices.Application.Compliance.EInvoice;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.EInvoices.Commands;

/// <summary>Generates (or regenerates) the invoice in every e-invoice format that applies; the result says which succeeded.</summary>
public sealed record GenerateInvoiceEInvoicesCommand(long InvoiceId) : IRequest<IReadOnlyList<EInvoiceGenerationDto>>;

public sealed class GenerateInvoiceEInvoicesCommandHandler : IRequestHandler<GenerateInvoiceEInvoicesCommand, IReadOnlyList<EInvoiceGenerationDto>>
{
    private readonly IEInvoiceService _eInvoices;

    public GenerateInvoiceEInvoicesCommandHandler(IEInvoiceService eInvoices)
    {
        _eInvoices = eInvoices;
    }

    /// <exception cref="KeyNotFoundException">The invoice does not exist.</exception>
    /// <exception cref="InvalidOperationException">The invoice is a draft or cancelled.</exception>
    public async ValueTask<IReadOnlyList<EInvoiceGenerationDto>> Handle(GenerateInvoiceEInvoicesCommand request, CancellationToken cancellationToken)
    {
        var results = await _eInvoices.GenerateAsync(request.InvoiceId, onlyMandatory: false, cancellationToken);
        return results.Select(EInvoiceMapper.ToDto).ToList();
    }
}
