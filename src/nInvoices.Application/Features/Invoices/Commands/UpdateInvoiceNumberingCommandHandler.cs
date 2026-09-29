using MediatR;
using Microsoft.Extensions.Options;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Application.Services;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed class UpdateInvoiceNumberingCommandHandler : IRequestHandler<UpdateInvoiceNumberingCommand, InvoiceNumberingDto>
{
    private readonly IRepository<InvoiceSequence> _sequences;
    private readonly IDraftInvoiceSynchronizer _drafts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InvoiceSettings _settings;

    public UpdateInvoiceNumberingCommandHandler(
        IRepository<InvoiceSequence> sequences,
        IDraftInvoiceSynchronizer drafts,
        IUnitOfWork unitOfWork,
        IOptions<InvoiceSettings> settings)
    {
        _sequences = sequences;
        _drafts = drafts;
        _unitOfWork = unitOfWork;
        _settings = settings.Value;
    }

    /// <exception cref="ArgumentException">The value or the pattern is not valid.</exception>
    public async Task<InvoiceNumberingDto> Handle(UpdateInvoiceNumberingCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Numbering;
        if (dto.Value < 1)
            throw new ArgumentException("Sequence value must be at least 1");

        // The query filter returns only the current user's row
        var sequence = (await _sequences.GetAllAsync(cancellationToken)).FirstOrDefault();
        if (sequence == null)
        {
            sequence = new InvoiceSequence(dto.Value);
            sequence.SetNumberFormat(dto.NumberFormat);
            await _sequences.AddAsync(sequence, cancellationToken);
        }
        else
        {
            sequence.SetValue(dto.Value);
            sequence.SetNumberFormat(dto.NumberFormat);
            await _sequences.UpdateAsync(sequence, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The drafts show the next number, which the new value or pattern has just changed
        await _drafts.RefreshDraftsAsync(cancellationToken);

        return InvoiceNumberingMapper.ToDto(sequence, _settings.NumberFormat);
    }
}
