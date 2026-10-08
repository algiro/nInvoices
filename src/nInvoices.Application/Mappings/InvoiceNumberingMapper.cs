using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Mappings;

public static class InvoiceNumberingMapper
{
    private const string SampleCustomerCode = "ACME";

    /// <param name="sequence">The user's numbering, or null when they have not issued an invoice yet.</param>
    /// <param name="today">The date the sample next number is shown for.</param>
    public static InvoiceNumberingDto ToDto(InvoiceSequence? sequence, string defaultFormat, DateTime today)
    {
        var currentValue = sequence?.CurrentValue ?? 1;
        var format = sequence?.NumberFormat ?? defaultFormat;
        var next = InvoiceNumber.Generate(format, currentValue, today, SampleCustomerCode);

        return new InvoiceNumberingDto(currentValue, format, sequence?.NumberFormat, defaultFormat, next.Value);
    }
}
