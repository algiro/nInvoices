using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Data transfer object for generating a new invoice.
/// Contains all information needed for invoice creation.
/// </summary>
public sealed record GenerateInvoiceDto(
    long CustomerId,
    InvoiceType Type,
    int Year,
    int Month,
    IEnumerable<WorkDayDto> WorkDays,
    IEnumerable<ExpenseDto> Expenses,
    string InvoiceNumberFormat = "INV-{YEAR}-{NUMBER:000}");