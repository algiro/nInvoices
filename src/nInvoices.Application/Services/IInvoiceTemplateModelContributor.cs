using nInvoices.Application.Models;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Services;

/// <summary>
/// Adds to the model an invoice template renders from, for what country rules put on the invoice
/// document (the Verifactu QR code, say). A contributor adds its data under <see cref="InvoiceTemplateModel.Compliance"/>
/// and does nothing for a user the rules do not apply to.
/// </summary>
public interface IInvoiceTemplateModelContributor
{
    Task<InvoiceTemplateModel> ContributeAsync(Invoice invoice, InvoiceTemplateModel model, CancellationToken cancellationToken);
}
