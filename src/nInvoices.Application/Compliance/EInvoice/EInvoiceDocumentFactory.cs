using nInvoices.Application.Exceptions;
using nInvoices.Application.Localization;
using nInvoices.Application.Services;
using nInvoices.Core.Compliance.EInvoice;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Compliance.EInvoice;

/// <summary>Turns a saved invoice into the country-neutral document an e-invoice format builds from.</summary>
public interface IEInvoiceDocumentFactory
{
    /// <param name="settings">The user's settings for the country the document is for (the issuer and the customer values come from them).</param>
    /// <exception cref="KeyNotFoundException">The invoice does not exist.</exception>
    Task<EInvoiceDocument> CreateAsync(long invoiceId, ComplianceSettings settings, CancellationToken cancellationToken = default);
}

public sealed class EInvoiceDocumentFactory : IEInvoiceDocumentFactory
{
    private readonly IInvoiceRepository _invoices;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<Tax> _taxes;
    private readonly IInvoiceGenerationService _generation;
    private readonly IAppText _text;

    public EInvoiceDocumentFactory(
        IInvoiceRepository invoices,
        IRepository<Customer> customers,
        IRepository<Tax> taxes,
        IInvoiceGenerationService generation,
        IAppText text)
    {
        _invoices = invoices;
        _customers = customers;
        _taxes = taxes;
        _generation = generation;
        _text = text;
    }

    public async Task<EInvoiceDocument> CreateAsync(long invoiceId, ComplianceSettings settings, CancellationToken cancellationToken = default)
    {
        var invoice = await _invoices.GetByIdWithRelatedAsync(invoiceId, cancellationToken)
            ?? throw new NotFoundException(_text.Translate("notFound.invoice", ("id", invoiceId)));
        var customer = await _customers.GetByIdAsync(invoice.CustomerId, cancellationToken)
            ?? throw new NotFoundException(_text.Translate("notFound.customer", ("id", invoice.CustomerId)));

        // The lines are the ones printed on the invoice, so the two documents always agree
        var model = await _generation.BuildTemplateModelAsync(invoiceId, cancellationToken);

        var lines = model.LineItems
            .Select(l => new EInvoiceLine(l.Description, l.Quantity, l.Rate, l.Amount))
            .ToList();

        var taxSettings = (await _taxes.GetAllAsync(cancellationToken)).ToList();

        var taxes = invoice.TaxLines
            .OrderBy(t => t.Order)
            .Select(t => new EInvoiceTax(
                t.IsWithholding ? EInvoiceTaxKind.Withheld : EInvoiceTaxKind.Added,
                t.Description,
                Math.Abs(t.Rate),
                t.BaseAmount.Amount,
                Math.Abs(t.TaxAmount.Amount),
                taxSettings.FirstOrDefault(x => x.CustomerId == invoice.CustomerId && x.TaxId == t.TaxId)?.GetComplianceValues(settings.CountryCode)))
            .ToList();

        return new EInvoiceDocument(
            invoice.Number.ToString(),
            invoice.IssueDate,
            invoice.DueDate,
            invoice.Total.Currency,
            settings.ToIssuerProfile(),
            new EInvoiceParty(customer.Name, customer.FiscalId, customer.Address, customer.GetComplianceValues(settings.CountryCode)),
            lines,
            taxes,
            invoice.Total.Amount,
            invoice.Notes,
            customer.Locale.Split('-', '_')[0]);
    }
}
