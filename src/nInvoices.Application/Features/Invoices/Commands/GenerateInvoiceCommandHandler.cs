using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Commands;

public sealed class GenerateInvoiceCommandHandler : IRequestHandler<GenerateInvoiceCommand, InvoiceDto>
{
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Invoice> _invoiceRepository;
    private readonly IInvoiceGenerationService _generationService;
    private readonly IUnitOfWork _unitOfWork;

    public GenerateInvoiceCommandHandler(
        IRepository<Customer> customerRepository,
        IRepository<Invoice> invoiceRepository,
        IInvoiceGenerationService generationService,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
        _invoiceRepository = invoiceRepository;
        _generationService = generationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<InvoiceDto> Handle(GenerateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Invoice;

        var customer = await _customerRepository.GetByIdAsync(dto.CustomerId, cancellationToken);
        if (customer == null)
            throw new KeyNotFoundException($"Customer with ID {dto.CustomerId} not found");

        var invoice = await _generationService.GenerateInvoiceAsync(
            customer,
            dto.Type,
            dto.Year,
            dto.Month,
            dto.WorkDays,
            dto.Expenses,
            dto.InvoiceNumberFormat,
            cancellationToken);

        await _invoiceRepository.AddAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(invoice);
    }

    private static InvoiceDto MapToDto(Invoice invoice) => new(
        invoice.Id,
        invoice.CustomerId,
        invoice.Type,
        invoice.InvoiceNumber.Value,
        invoice.IssueDate,
        invoice.DueDate,
        new MoneyDto(invoice.Subtotal.Amount, invoice.Subtotal.Currency),
        new MoneyDto(invoice.TotalExpenses.Amount, invoice.TotalExpenses.Currency),
        new MoneyDto(invoice.TotalTaxes.Amount, invoice.TotalTaxes.Currency),
        new MoneyDto(invoice.Total.Amount, invoice.Total.Currency),
        invoice.Status,
        invoice.RenderedContent,
        invoice.CreatedAt,
        invoice.UpdatedAt ?? invoice.CreatedAt);
}