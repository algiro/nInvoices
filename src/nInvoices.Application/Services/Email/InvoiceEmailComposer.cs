using nInvoices.Application.Models;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Services.Email;

/// <summary>Subject and body of an email template, with the template they came from.</summary>
/// <param name="TemplateId">Null when the built-in default was used.</param>
public sealed record EmailTemplateContent(long? TemplateId, string Name, string Subject, string Body);

/// <summary>A rendered email, or the errors that prevented rendering it.</summary>
public sealed record RenderedEmail(string? Subject, string? Html, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

/// <summary>
/// Builds the pieces of an invoice email: which template applies, the data it is rendered
/// with, the rendered subject/body, and the PDF attachments.
/// </summary>
public interface IInvoiceEmailComposer
{
    /// <summary>
    /// The requested template (it must belong to the customer), otherwise the customer's
    /// active one, otherwise the built-in default.
    /// </summary>
    Task<EmailTemplateContent> ResolveTemplateAsync(long customerId, long? templateId, CancellationToken cancellationToken = default);

    EmailTemplateModel BuildModel(Invoice invoice, Customer customer, string? senderEmail);

    Task<RenderedEmail> RenderAsync(string subject, string body, EmailTemplateModel model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a template being edited, using the customer's most recent invoice (or sample
    /// figures when the customer has none yet).
    /// </summary>
    Task<RenderedEmail> PreviewAsync(string subject, string body, long customerId, CancellationToken cancellationToken = default);

    /// <exception cref="InvoiceEmailException">A document could not be generated.</exception>
    Task<IReadOnlyList<EmailAttachment>> BuildAttachmentsAsync(
        Invoice invoice,
        Customer customer,
        bool includeMonthlyReport,
        CancellationToken cancellationToken = default);

    string InvoicePdfFileName(Invoice invoice);

    string MonthlyReportFileName(Invoice invoice, Customer customer);
}

public sealed class InvoiceEmailComposer : IInvoiceEmailComposer
{
    private const string PdfContentType = "application/pdf";

    private readonly IRepository<EmailTemplate> _templateRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Invoice> _invoiceRepository;
    private readonly ITemplateRenderer _renderer;
    private readonly ILocalizationService _localization;
    private readonly IPdfExportService _pdfExportService;
    private readonly IHtmlToPdfConverter _htmlToPdfConverter;
    private readonly IMonthlyReportGenerationService _monthlyReportService;

    public InvoiceEmailComposer(
        IRepository<EmailTemplate> templateRepository,
        IRepository<Customer> customerRepository,
        IRepository<Invoice> invoiceRepository,
        ITemplateRenderer renderer,
        ILocalizationService localization,
        IPdfExportService pdfExportService,
        IHtmlToPdfConverter htmlToPdfConverter,
        IMonthlyReportGenerationService monthlyReportService)
    {
        _templateRepository = templateRepository;
        _customerRepository = customerRepository;
        _invoiceRepository = invoiceRepository;
        _renderer = renderer;
        _localization = localization;
        _pdfExportService = pdfExportService;
        _htmlToPdfConverter = htmlToPdfConverter;
        _monthlyReportService = monthlyReportService;
    }

    public async Task<EmailTemplateContent> ResolveTemplateAsync(long customerId, long? templateId, CancellationToken cancellationToken = default)
    {
        if (templateId.HasValue)
        {
            var requested = await _templateRepository.GetByIdAsync(templateId.Value, cancellationToken);
            if (requested is not null && requested.CustomerId == customerId)
                return ToContent(requested);
        }

        var templates = await _templateRepository.FindAsync(t => t.CustomerId == customerId && t.IsActive, cancellationToken);
        var active = templates.OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt).FirstOrDefault();

        return active is not null
            ? ToContent(active)
            : new EmailTemplateContent(null, "Default", DefaultEmailTemplate.Subject, DefaultEmailTemplate.Body);

        static EmailTemplateContent ToContent(EmailTemplate t) => new(t.Id, t.Name, t.Subject, t.Body);
    }

    public EmailTemplateModel BuildModel(Invoice invoice, Customer customer, string? senderEmail)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(customer);

        return new EmailTemplateModel
        {
            InvoiceNumber = invoice.Number.ToString(),
            InvoiceType = invoice.Type.ToString(),
            Date = invoice.IssueDate.ToDateTime(TimeOnly.MinValue),
            DueDate = invoice.DueDate?.ToDateTime(TimeOnly.MinValue),
            Currency = invoice.Total.Currency,
            Subtotal = invoice.Subtotal.Amount,
            TotalTax = invoice.TotalTaxes.Amount,
            TotalExpenses = invoice.TotalExpenses.Amount,
            Total = invoice.Total.Amount,
            WorkedDays = invoice.WorkedDays,
            Year = invoice.Year,
            MonthNumber = invoice.Month,
            MonthDescription = invoice.Month is { } month ? _localization.GetMonthName(month, customer.Locale) : null,
            Locale = customer.Locale,
            Customer = CustomerModel(customer),
            SenderEmail = senderEmail ?? string.Empty
        };
    }

    public async Task<RenderedEmail> RenderAsync(string subject, string body, EmailTemplateModel model, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var renderedSubject = await RenderPartAsync("Subject", subject, model, errors, cancellationToken);
        var renderedBody = await RenderPartAsync("Body", body, model, errors, cancellationToken);

        return errors.Count > 0
            ? new RenderedEmail(null, null, errors)
            // A subject is a single line; template line breaks would corrupt the header
            : new RenderedEmail(CollapseWhitespace(renderedSubject!), renderedBody, []);
    }

    public async Task<RenderedEmail> PreviewAsync(string subject, string body, long customerId, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
            return new RenderedEmail(null, null, [$"Customer {customerId} not found"]);

        var invoices = await _invoiceRepository.FindAsync(i => i.CustomerId == customerId, cancellationToken);
        var latest = invoices
            .Where(i => i.Status != InvoiceStatus.Cancelled)
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.Id)
            .FirstOrDefault();

        var model = latest is not null
            ? BuildModel(latest, customer, "you@gmail.com")
            : SampleModel(customer);

        return await RenderAsync(subject, body, model, cancellationToken);
    }

    public async Task<IReadOnlyList<EmailAttachment>> BuildAttachmentsAsync(
        Invoice invoice,
        Customer customer,
        bool includeMonthlyReport,
        CancellationToken cancellationToken = default)
    {
        var attachments = new List<EmailAttachment>();

        try
        {
            // Same output as GET /api/invoices/{id}/pdf: the stored rendered template, else the built-in layout
            var invoicePdf = string.IsNullOrWhiteSpace(invoice.RenderedContent)
                ? _pdfExportService.GenerateInvoicePdf(invoice)
                : await _htmlToPdfConverter.ConvertAsync(invoice.RenderedContent, cancellationToken);
            attachments.Add(new EmailAttachment(InvoicePdfFileName(invoice), PdfContentType, invoicePdf));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvoiceEmailException(InvoiceEmailException.AttachmentFailed, "The invoice PDF could not be generated.", ex);
        }

        if (includeMonthlyReport && invoice.Type == InvoiceType.Monthly)
        {
            try
            {
                var html = await _monthlyReportService.GenerateReportHtmlAsync(invoice, customer, cancellationToken);
                var reportPdf = await _htmlToPdfConverter.ConvertAsync(html, cancellationToken);
                attachments.Add(new EmailAttachment(MonthlyReportFileName(invoice, customer), PdfContentType, reportPdf));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var reason = ex is InvalidOperationException ? $" {ex.Message}" : string.Empty;
                throw new InvoiceEmailException(
                    InvoiceEmailException.AttachmentFailed,
                    $"The monthly report PDF could not be generated.{reason}",
                    ex);
            }
        }

        return attachments;
    }

    public string InvoicePdfFileName(Invoice invoice) => SafeFileName($"Invoice-{invoice.Number}.pdf");

    public string MonthlyReportFileName(Invoice invoice, Customer customer) =>
        SafeFileName($"MonthlyReport-{invoice.Year}-{invoice.Month:00}-{customer.Name}.pdf");

    private async Task<string?> RenderPartAsync(string part, string content, EmailTemplateModel model, List<string> errors, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add($"{part}: cannot be empty");
            return null;
        }

        var validation = await _renderer.ValidateAsync(content, cancellationToken);
        if (!validation.IsValid)
        {
            errors.AddRange(validation.Errors.Select(e => $"{part}: {e}"));
            return null;
        }

        try
        {
            return await _renderer.RenderAsync(content, model, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // The renderer wraps Scriban's runtime error (e.g. an unknown function or a bad argument)
            errors.Add($"{part}: {ex.InnerException?.Message ?? ex.Message}");
            return null;
        }
    }

    private EmailTemplateModel SampleModel(Customer customer)
    {
        var lastMonth = DateTime.Today.AddMonths(-1);
        return new EmailTemplateModel
        {
            InvoiceNumber = $"{lastMonth:yy}-{lastMonth:MM}-001",
            InvoiceType = nameof(InvoiceType.Monthly),
            Date = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            Currency = "EUR",
            Subtotal = 10000m,
            TotalTax = 2200m,
            Total = 12200m,
            WorkedDays = 20,
            Year = lastMonth.Year,
            MonthNumber = lastMonth.Month,
            MonthDescription = _localization.GetMonthName(lastMonth.Month, customer.Locale),
            Locale = customer.Locale,
            Customer = CustomerModel(customer),
            SenderEmail = "you@gmail.com"
        };
    }

    private static EmailCustomerModel CustomerModel(Customer customer) => new()
    {
        Name = customer.Name,
        FiscalId = customer.FiscalId,
        Email = customer.Email ?? string.Empty,
        Address = new AddressTemplateModel
        {
            Street = customer.Address?.Street ?? string.Empty,
            City = customer.Address?.City ?? string.Empty,
            PostalCode = customer.Address?.ZipCode ?? string.Empty,
            Country = customer.Address?.Country ?? string.Empty
        }
    };

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
