using nInvoices.Application.Compliance;
using nInvoices.Application.Features.Invoices;
using nInvoices.Application.Localization;
using nInvoices.Core.Entities;
using nInvoices.Core.Exceptions;
using nInvoices.Core.Interfaces;

namespace nInvoices.Countries.UnitedKingdom;

/// <summary>
/// Before an invoice is issued under the United Kingdom's rules: a VAT-registered business's invoice is a VAT
/// invoice, so a 0% VAT says why and the customer's address is on it; a business that isn't registered charges no
/// VAT. Does nothing without the UK rules.
/// </summary>
public sealed class UkVatInvoiceStep : IInvoiceLifecycleStep
{
    private readonly IComplianceGate _gate;
    private readonly IRepository<Tax> _taxes;
    private readonly IAppText _text;

    public UkVatInvoiceStep(IComplianceGate gate, IRepository<Tax> taxes, IAppText text)
    {
        _gate = gate;
        _taxes = taxes;
        _text = text;
    }

    public async Task<bool> OnFinalizingAsync(Invoice invoice, Customer customer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(customer);

        var vat = await UkInvoiceVat.OfAsync(invoice, _gate, _taxes, cancellationToken);
        if (vat.Issuer is null)
            return false;

        if (!vat.IsVatRegistered)
        {
            if (vat.Vat.Any(v => v.Line.TaxAmount.Amount > 0))
                throw new DomainException(_text.Translate("compliance.uk.notRegisteredChargesVat"));
            return false;
        }

        if (vat.Vat.FirstOrDefault(v => v.Line.Rate == 0 && v.Treatment is null) is { Line: { } line })
            throw new DomainException(_text.Translate("compliance.uk.zeroVatNeedsTreatment", ("tax", line.Description)));

        if (string.IsNullOrWhiteSpace(customer.Name) || string.IsNullOrWhiteSpace(customer.Address.Street) || string.IsNullOrWhiteSpace(customer.Address.City))
            throw new DomainException(_text.Translate("compliance.uk.customerAddressRequired"));

        return false;
    }

    public Task OnCancellingAsync(Invoice invoice, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnDeletingAsync(Invoice invoice, CancellationToken cancellationToken) => Task.CompletedTask;
}
