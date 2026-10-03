using MediatR;
using Microsoft.Extensions.Logging;
using nInvoices.Application.Features.Invoices.Notifications;

namespace nInvoices.Application.Compliance.EInvoice;

/// <summary>
/// When an invoice is finalized, generates the e-invoices the rules require for its customer
/// (Facturae for a public administration). A problem is logged and left for the invoice screen to
/// show, never raised: the invoice is already final and has used up its number.
/// </summary>
public sealed class GenerateMandatoryEInvoices : INotificationHandler<InvoiceFinalizedNotification>
{
    private readonly IEInvoiceService _eInvoices;
    private readonly ILogger<GenerateMandatoryEInvoices> _logger;

    public GenerateMandatoryEInvoices(IEInvoiceService eInvoices, ILogger<GenerateMandatoryEInvoices> logger)
    {
        _eInvoices = eInvoices;
        _logger = logger;
    }

    public async Task Handle(InvoiceFinalizedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var results = await _eInvoices.GenerateAsync(notification.InvoiceId, onlyMandatory: true, cancellationToken);

            foreach (var result in results.Where(r => !r.Generated))
            {
                _logger.LogWarning("Invoice {InvoiceId}: {Format} could not be generated: {Issues}",
                    notification.InvoiceId, result.Format.FormatId, string.Join("; ", result.Issues.Select(i => i.Message)));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Invoice {InvoiceId}: generating the e-invoice failed", notification.InvoiceId);
        }
    }
}
