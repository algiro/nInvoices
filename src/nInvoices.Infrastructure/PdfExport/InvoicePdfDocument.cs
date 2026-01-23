using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.PdfExport;

/// <summary>
/// Professional invoice PDF document generator.
/// Uses QuestPDF's fluent API for declarative document composition.
/// Follows Single Responsibility Principle - only handles invoice PDF layout.
/// </summary>
public sealed class InvoicePdfDocument : IDocument
{
    private readonly Invoice _invoice;

    public InvoicePdfDocument(Invoice invoice)
    {
        _invoice = invoice ?? throw new ArgumentNullException(nameof(invoice));
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("INVOICE")
                    .FontSize(28)
                    .Bold()
                    .FontColor(Colors.Blue.Darken3);

                column.Item().PaddingTop(10).Text(text =>
                {
                    text.Span("Invoice #: ").Bold();
                    text.Span(_invoice.Number.ToString());
                });

                column.Item().Text(text =>
                {
                    text.Span("Date: ").Bold();
                    text.Span(_invoice.IssueDate.ToString("MMM dd, yyyy"));
                });

                if (_invoice.DueDate.HasValue)
                {
                    column.Item().Text(text =>
                    {
                        text.Span("Due Date: ").Bold();
                        text.Span(_invoice.DueDate.Value.ToString("MMM dd, yyyy"));
                    });
                }

                column.Item().Text(text =>
                {
                    text.Span("Status: ").Bold();
                    text.Span(_invoice.Status.ToString())
                        .FontColor(GetStatusColor(_invoice.Status));
                });
            });

            row.ConstantItem(150).AlignRight().Column(column =>
            {
                column.Item().Text("Total Amount")
                    .FontSize(12)
                    .Bold();
                
                column.Item().Text(_invoice.Total.ToString())
                    .FontSize(24)
                    .Bold()
                    .FontColor(Colors.Blue.Darken3);
            });
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(20).Column(column =>
        {
            column.Spacing(15);

            column.Item().Element(ComposeCustomerInfo);

            if (_invoice.Type == Core.Enums.InvoiceType.Monthly && _invoice.WorkedDays.HasValue)
            {
                column.Item().Element(ComposeMonthlyDetails);
            }

            column.Item().Element(ComposeItemsTable);

            if (_invoice.Expenses.Any())
            {
                column.Item().Element(ComposeExpensesTable);
            }

            if (_invoice.TaxLines.Any())
            {
                column.Item().Element(ComposeTaxBreakdown);
            }

            column.Item().Element(ComposeTotals);

            if (!string.IsNullOrWhiteSpace(_invoice.Notes))
            {
                column.Item().Element(ComposeNotes);
            }
        });
    }

    private void ComposeCustomerInfo(IContainer container)
    {
        container.Background(Colors.Grey.Lighten3).Padding(10).Column(column =>
        {
            column.Item().Text("Bill To:").Bold().FontSize(12);
            column.Item().PaddingTop(5).Text(_invoice.Customer?.Name ?? "N/A").FontSize(11);
            
            if (_invoice.Customer?.Address != null)
            {
                var addr = _invoice.Customer.Address;
                column.Item().Text($"{addr.Street} {addr.HouseNumber}").FontSize(10);
                column.Item().Text($"{addr.ZipCode} {addr.City}").FontSize(10);
                if (!string.IsNullOrEmpty(addr.State))
                    column.Item().Text(addr.State).FontSize(10);
                column.Item().Text(addr.Country).FontSize(10);
            }

            if (!string.IsNullOrWhiteSpace(_invoice.Customer?.FiscalId))
            {
                column.Item().PaddingTop(5).Text(text =>
                {
                    text.Span("Tax ID: ").Bold();
                    text.Span(_invoice.Customer.FiscalId);
                });
            }
        });
    }

    private void ComposeMonthlyDetails(IContainer container)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.Span("Period: ").Bold();
                text.Span($"{GetMonthName(_invoice.Month!.Value)} {_invoice.Year}");
            });

            row.RelativeItem().Text(text =>
            {
                text.Span("Worked Days: ").Bold();
                text.Span(_invoice.WorkedDays!.Value.ToString());
            });
        });
    }

    private void ComposeItemsTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn(2);
                columns.RelativeColumn(1);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                    .Text("Description").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                    .Text("Rate").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                    .AlignRight().Text("Qty").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                    .AlignRight().Text("Amount").FontColor(Colors.White).Bold();
            });

            var description = _invoice.Type == Core.Enums.InvoiceType.Monthly 
                ? $"Professional Services - {GetMonthName(_invoice.Month!.Value)} {_invoice.Year}"
                : "Professional Services";

            var rate = _invoice.Subtotal.Amount / (_invoice.WorkedDays ?? 1);
            var quantity = _invoice.WorkedDays ?? 1;

            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                .Text(description);
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                .Text($"{rate:N2} {_invoice.Subtotal.Currency}");
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                .AlignRight().Text(quantity.ToString());
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                .AlignRight().Text(_invoice.Subtotal.ToString()).Bold();
        });
    }

    private void ComposeExpensesTable(IContainer container)
    {
        container.PaddingTop(10).Column(column =>
        {
            column.Item().Text("Expenses").Bold().FontSize(12);

            column.Item().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5)
                        .Text("Date").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5)
                        .Text("Description").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5)
                        .AlignRight().Text("Amount").Bold();
                });

                foreach (var expense in _invoice.Expenses)
                {
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .Text(expense.Date.ToString("MMM dd, yyyy"));
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .Text(expense.Description);
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .AlignRight().Text(expense.Amount.ToString());
                }
            });
        });
    }

    private void ComposeTaxBreakdown(IContainer container)
    {
        container.PaddingTop(10).Column(column =>
        {
            column.Item().Text("Tax Breakdown").Bold().FontSize(12);

            column.Item().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5)
                        .Text("Tax").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5)
                        .AlignRight().Text("Rate").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5)
                        .AlignRight().Text("Amount").Bold();
                });

                foreach (var taxLine in _invoice.TaxLines)
                {
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .Text(taxLine.Description);
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .AlignRight().Text($"{taxLine.Rate * 100:N2}%");
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .AlignRight().Text(taxLine.TaxAmount.ToString());
                }
            });
        });
    }

    private void ComposeTotals(IContainer container)
    {
        container.AlignRight().Width(250).Column(column =>
        {
            column.Spacing(5);

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Subtotal:");
                row.ConstantItem(100).AlignRight().Text(_invoice.Subtotal.ToString());
            });

            if (_invoice.TotalExpenses.Amount > 0)
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Total Expenses:");
                    row.ConstantItem(100).AlignRight().Text(_invoice.TotalExpenses.ToString());
                });
            }

            if (_invoice.TotalTaxes.Amount > 0)
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Total Taxes:");
                    row.ConstantItem(100).AlignRight().Text(_invoice.TotalTaxes.ToString());
                });
            }

            column.Item().PaddingTop(5).Border(1).BorderColor(Colors.Blue.Darken3)
                .Background(Colors.Blue.Lighten4).Padding(5).Row(row =>
                {
                    row.RelativeItem().Text("TOTAL:").Bold().FontSize(12);
                    row.ConstantItem(100).AlignRight().Text(_invoice.Total.ToString())
                        .Bold().FontSize(12);
                });
        });
    }

    private void ComposeNotes(IContainer container)
    {
        container.PaddingTop(15).Border(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(10).Column(column =>
            {
                column.Item().Text("Notes:").Bold();
                column.Item().PaddingTop(5).Text(_invoice.Notes);
            });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Generated on ").FontSize(8).FontColor(Colors.Grey.Darken1);
            text.Span(DateTime.UtcNow.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Darken1);
            text.Span(" UTC").FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    private static string GetStatusColor(Core.Enums.InvoiceStatus status)
    {
        return status switch
        {
            Core.Enums.InvoiceStatus.Draft => Colors.Orange.Medium,
            Core.Enums.InvoiceStatus.Finalized => Colors.Blue.Medium,
            Core.Enums.InvoiceStatus.Sent => Colors.Indigo.Medium,
            Core.Enums.InvoiceStatus.Paid => Colors.Green.Medium,
            Core.Enums.InvoiceStatus.Cancelled => Colors.Red.Medium,
            _ => Colors.Grey.Medium
        };
    }

    private static string GetMonthName(int month)
    {
        return new DateTime(2000, month, 1).ToString("MMMM");
    }
}
