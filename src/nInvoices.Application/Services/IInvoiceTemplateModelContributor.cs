using nInvoices.Application.Models;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Services;

/// <summary>
/// Adds to the model an invoice template renders from what country rules put on the invoice document (the
/// Verifactu QR code, say), under <see cref="InvoiceTemplateModel.Compliance"/>[<see cref="Key"/>]. The entry is
/// always there: <see cref="Empty"/> when the rules don't apply to the invoice or the user, so templates never
/// fail on it.
/// </summary>
public interface IInvoiceTemplateModelContributor
{
    /// <summary>The name templates find the contribution under: <c>compliance.&lt;key&gt;</c>.</summary>
    string Key { get; }

    /// <summary>What templates see when there is nothing to show: every member a template may print, empty.</summary>
    object Empty { get; }

    /// <summary>A filled-in example, for the template editor's preview.</summary>
    object Sample { get; }

    /// <returns>The contribution for the invoice, or null when the rules give it none (<see cref="Empty"/> is used).</returns>
    Task<object?> ContributeAsync(Invoice invoice, CancellationToken cancellationToken);
}
