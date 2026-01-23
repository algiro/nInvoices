using MediatR;
using Microsoft.Extensions.Logging;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed class GenerateInvoiceCommandHandler : IRequestHandler<GenerateInvoiceCommand, InvoiceDto>
{
    private readonly IInvoiceGenerationService _invoiceGenerationService;
    private readonly IRepository<Invoice> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GenerateInvoiceCommandHandler> _logger;

    public GenerateInvoiceCommandHandler(
        IInvoiceGenerationService invoiceGenerationService,
        IRepository<Invoice> repository,
        IUnitOfWork unitOfWork,
        ILogger<GenerateInvoiceCommandHandler> logger)
    {
        _invoiceGenerationService = invoiceGenerationService;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<InvoiceDto> Handle(GenerateInvoiceCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Generating invoice for customer {CustomerId}, type {InvoiceType}",
            request.Invoice.CustomerId,
            request.Invoice.InvoiceType);

        var invoice = await _invoiceGenerationService.GenerateInvoiceAsync(
            request.Invoice,
            cancellationToken);

        await _repository.AddAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Invoice {InvoiceNumber} generated successfully with ID {InvoiceId}",
            invoice.Number,
            invoice.Id);

        return InvoiceMapper.ToDto(invoice);
    }
}
