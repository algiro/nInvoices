using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceEmails.Queries;

public sealed class GetInvoiceEmailComposeQueryHandler : IRequestHandler<GetInvoiceEmailComposeQuery, InvoiceEmailComposeDto?>
{
    public const string InvoiceAttachmentKey = "invoice";
    public const string MonthlyReportAttachmentKey = "monthlyReport";

    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<EmailTemplate> _templateRepository;
    private readonly IRepository<GmailConnection> _connectionRepository;
    private readonly IInvoiceEmailComposer _composer;
    private readonly IUserContext _userContext;

    public GetInvoiceEmailComposeQueryHandler(
        IInvoiceRepository invoiceRepository,
        IRepository<Customer> customerRepository,
        IRepository<EmailTemplate> templateRepository,
        IRepository<GmailConnection> connectionRepository,
        IInvoiceEmailComposer composer,
        IUserContext userContext)
    {
        _invoiceRepository = invoiceRepository;
        _customerRepository = customerRepository;
        _templateRepository = templateRepository;
        _connectionRepository = connectionRepository;
        _composer = composer;
        _userContext = userContext;
    }

    public async Task<InvoiceEmailComposeDto?> Handle(GetInvoiceEmailComposeQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByIdWithRelatedAsync(request.InvoiceId, cancellationToken);
        if (invoice is null)
            return null;

        var customer = await _customerRepository.GetByIdAsync(invoice.CustomerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer {invoice.CustomerId} not found");

        var userId = _userContext.RequireUserId();
        var connection = (await _connectionRepository.FindAsync(c => c.UserId == userId, cancellationToken)).FirstOrDefault();

        var template = await _composer.ResolveTemplateAsync(customer.Id, request.TemplateId, cancellationToken);
        var model = _composer.BuildModel(invoice, customer, connection?.EmailAddress);
        var rendered = await _composer.RenderAsync(template.Subject, template.Body, model, cancellationToken);

        var customerTemplates = await _templateRepository.FindAsync(t => t.CustomerId == customer.Id, cancellationToken);
        var options = customerTemplates
            .OrderByDescending(t => t.IsActive)
            .ThenBy(t => t.Name)
            .Select(t => new EmailTemplateOptionDto(t.Id, t.Name, t.IsActive))
            .ToList();
        if (template.TemplateId is null)
            options.Insert(0, new EmailTemplateOptionDto(null, "Default (built-in)", options.Count == 0));

        var attachments = new List<EmailAttachmentOptionDto>
        {
            new(InvoiceAttachmentKey, _composer.InvoicePdfFileName(invoice), true)
        };
        if (invoice.Type == InvoiceType.Monthly)
            attachments.Add(new(MonthlyReportAttachmentKey, _composer.MonthlyReportFileName(invoice, customer), true));

        return new InvoiceEmailComposeDto(
            invoice.Id,
            template.TemplateId,
            options,
            connection?.EmailAddress,
            customer.Email ?? string.Empty,
            customer.CcEmails,
            // When the template fails to render, hand back its source so the problem can be seen and fixed
            rendered.Subject ?? template.Subject,
            rendered.Html ?? template.Body,
            attachments,
            rendered.Errors);
    }
}
