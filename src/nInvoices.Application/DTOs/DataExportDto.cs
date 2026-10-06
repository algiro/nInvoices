using nInvoices.Core.Enums;

namespace nInvoices.Application.DTOs;

/// <summary>
/// Complete customer export with all related data.
/// </summary>
public sealed record CustomerExportDto(
    string Name,
    string FiscalId,
    AddressDto Address,
    DateTime CreatedAt,
    IReadOnlyList<RateExportDto> Rates,
    IReadOnlyList<TaxExportDto> Taxes,
    IReadOnlyList<InvoiceTemplateExportDto> InvoiceTemplates,
    IReadOnlyList<MonthlyReportTemplateExportDto> MonthlyReportTemplates,
    string? Email = null,
    string? CcEmails = null,
    IReadOnlyList<EmailTemplateExportDto>? EmailTemplates = null,
    string? HolidayCountry = null,
    // The fields below are absent from older exports: they import as empty / default
    string? Locale = null,
    IReadOnlyList<ProjectExportDto>? Projects = null,
    IReadOnlyList<WorkDayExportDto>? WorkDays = null,
    IReadOnlyDictionary<string, string>? ComplianceValues = null,
    // Expenses not on an invoice yet (those on one are exported with their invoice)
    IReadOnlyList<ExpenseDto>? UnbilledExpenses = null);

public sealed record ProjectExportDto(string Name, bool IsActive);

/// <summary>
/// A day in the customer's calendar. <see cref="RateIndex"/> is the position of the day's rate in
/// the customer's <see cref="CustomerExportDto.Rates"/> (null: the customer's default rate).
/// </summary>
public sealed record WorkDayExportDto(
    DateOnly Date,
    DayType DayType,
    decimal? HoursWorked,
    string? Notes,
    int? RateIndex,
    IReadOnlyList<WorkDayProjectExportDto> Projects);

public sealed record WorkDayProjectExportDto(string ProjectName, decimal Hours);

public sealed record RateExportDto(
    RateType Type,
    MoneyDto Price,
    DateTime CreatedAt,
    string? Name = null,
    bool IsActive = true);

public sealed record TaxExportDto(
    string TaxId,
    string Description,
    string HandlerId,
    decimal Rate,
    TaxApplicationType ApplicationType,
    string? AppliedToTaxId,
    int Order,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyDictionary<string, string>? ComplianceValues = null);

public sealed record InvoiceTemplateExportDto(
    InvoiceType InvoiceType,
    string Name,
    string Content,
    bool IsActive,
    DateTime CreatedAt);

public sealed record EmailTemplateExportDto(
    string Name,
    string Subject,
    string Body,
    bool IsActive,
    DateTime CreatedAt);

public sealed record MonthlyReportTemplateExportDto(
    InvoiceType InvoiceType,
    string Name,
    string Content,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>
/// Complete invoice export with line items and expenses.
/// </summary>
public sealed record InvoiceExportDto(
    string CustomerFiscalId,
    InvoiceType Type,
    string InvoiceNumber,
    DateOnly IssueDate,
    DateOnly? DueDate,
    int? WorkedDays,
    int? Year,
    int? Month,
    MoneyDto Subtotal,
    MoneyDto TotalExpenses,
    MoneyDto TotalTaxes,
    MoneyDto Total,
    InvoiceStatus Status,
    string? RenderedContent,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<ExpenseDto> Expenses,
    IReadOnlyList<InvoiceTaxLineExportDto> TaxLines,
    // Absent from older exports. RateIndex: position in the customer's exported rates, ordered by id
    decimal? Hours = null,
    int? RateIndex = null);

public sealed record InvoiceTaxLineExportDto(
    string TaxId,
    string Description,
    decimal Rate,
    decimal BaseAmount,
    decimal TaxAmount,
    int Order);

/// <summary>
/// The templates shared by all of the user's customers (they belong to no customer).
/// </summary>
public sealed record SharedTemplatesExportDto(
    IReadOnlyList<InvoiceTemplateExportDto> InvoiceTemplates,
    IReadOnlyList<MonthlyReportTemplateExportDto> MonthlyReportTemplates,
    IReadOnlyList<EmailTemplateExportDto> EmailTemplates);

/// <summary>
/// Root export container with metadata. <see cref="SharedTemplates"/> is absent from exports
/// made before templates could be shared.
/// </summary>
public sealed record DataExportDto(
    string ExportVersion,
    DateTime ExportedAt,
    IReadOnlyList<CustomerExportDto>? Customers,
    IReadOnlyList<InvoiceExportDto>? Invoices,
    SharedTemplatesExportDto? SharedTemplates = null,
    UserSettingsExportDto? Settings = null);

/// <summary>
/// The user's own settings and assets. Not included: the e-invoicing signing certificate (it is
/// encrypted with the server's keys, so it has to be uploaded again), and the server-wide settings.
/// </summary>
public sealed record UserSettingsExportDto(
    InvoiceNumberingExportDto? InvoiceNumbering,
    IReadOnlyList<ImageAssetExportDto> Images,
    IReadOnlyList<HolidayCalendarExportDto> HolidayCalendars,
    IReadOnlyList<ComplianceSettingsExportDto> Compliance);

/// <param name="NextNumber">The number the next finalized invoice takes.</param>
public sealed record InvoiceNumberingExportDto(int NextNumber, string? NumberFormat);

public sealed record ImageAssetExportDto(string Alias, string FileName, string ContentType, string Base64Data, long FileSize);

/// <summary>A calendar the user changed; calendars left as built in are not exported.</summary>
public sealed record HolidayCalendarExportDto(string CountryCode, IReadOnlyList<HolidayRuleExportDto> Rules);

public sealed record HolidayRuleExportDto(
    string Name,
    HolidayRuleKind Kind,
    int? Month,
    int? Day,
    int? EasterOffset,
    DayOfWeek? Weekday,
    int? Occurrence,
    int? FromYear,
    int? ToYear,
    bool IsActive);

public sealed record ComplianceSettingsExportDto(
    string CountryCode,
    bool IsEnabled,
    string? LegalName,
    string? TaxId,
    AddressDto? Address,
    IReadOnlyDictionary<string, string> Values);

/// <summary>What an import did: items imported, items skipped (already there) and the problems, one line per item.</summary>
public sealed record ImportResultDto(int Imported, int Skipped, IReadOnlyList<string> Errors);
