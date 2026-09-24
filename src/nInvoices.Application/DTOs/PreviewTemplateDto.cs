namespace nInvoices.Application.DTOs;

/// <summary>
/// Request to render a template with sample data. When a customer is given, the sample uses
/// that customer's name, address, locale, rate and taxes.
/// </summary>
public sealed record PreviewTemplateDto(string Content, long? CustomerId = null);
