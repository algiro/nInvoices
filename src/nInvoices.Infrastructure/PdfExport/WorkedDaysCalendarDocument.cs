using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.PdfExport;

/// <summary>
/// Worked days calendar PDF document for monthly invoices.
/// Provides visual representation of worked days for timesheet documentation.
/// </summary>
public sealed class WorkedDaysCalendarDocument : IDocument
{
    private readonly Invoice _invoice;
    private readonly List<DateOnly> _workedDates;

    public WorkedDaysCalendarDocument(Invoice invoice)
    {
        _invoice = invoice ?? throw new ArgumentNullException(nameof(invoice));

        if (_invoice.Type != Core.Enums.InvoiceType.Monthly)
            throw new InvalidOperationException("Calendar is only available for monthly invoices");

        _workedDates = new List<DateOnly>();
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(30);
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
                column.Item().Text("WORKED DAYS CALENDAR")
                    .FontSize(24)
                    .Bold()
                    .FontColor(Colors.Blue.Darken3);

                column.Item().PaddingTop(5).Text(text =>
                {
                    text.Span("Period: ").Bold();
                    text.Span($"{GetMonthName(_invoice.Month!.Value)} {_invoice.Year}");
                });

                column.Item().Text(text =>
                {
                    text.Span("Customer: ").Bold();
                    text.Span(_invoice.Customer?.Name ?? "N/A");
                });

                column.Item().Text(text =>
                {
                    text.Span("Invoice: ").Bold();
                    text.Span(_invoice.Number.ToString());
                });
            });

            row.ConstantItem(150).AlignRight().Column(column =>
            {
                column.Item().Text("Total Days")
                    .FontSize(12)
                    .Bold();
                
                column.Item().Text(_invoice.WorkedDays?.ToString() ?? "0")
                    .FontSize(32)
                    .Bold()
                    .FontColor(Colors.Blue.Darken3);
            });
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(20).Column(column =>
        {
            column.Spacing(10);
            column.Item().Element(ComposeCalendar);
            column.Item().Element(ComposeLegend);
        });
    }

    private void ComposeCalendar(IContainer container)
    {
        var year = _invoice.Year!.Value;
        var month = _invoice.Month!.Value;
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var firstDay = new DateTime(year, month, 1);
        var startDayOfWeek = (int)firstDay.DayOfWeek;

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                for (int i = 0; i < 7; i++)
                    columns.RelativeColumn();
            });

            table.Header(header =>
            {
                string[] dayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
                foreach (var dayName in dayNames)
                {
                    header.Cell().Background(Colors.Blue.Darken3).Padding(8)
                        .AlignCenter().Text(dayName).FontColor(Colors.White).Bold();
                }
            });

            var currentDay = 1;
            var totalCells = (int)Math.Ceiling((startDayOfWeek + daysInMonth) / 7.0) * 7;

            for (int cellIndex = 0; cellIndex < totalCells; cellIndex++)
            {
                if (cellIndex < startDayOfWeek || currentDay > daysInMonth)
                {
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2)
                        .Height(60).Background(Colors.Grey.Lighten4);
                }
                else
                {
                    var date = new DateOnly(year, month, currentDay);
                    var isWorked = _workedDates.Contains(date);
                    var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2)
                        .Height(60)
                        .Background(isWorked ? Colors.Green.Lighten3 : 
                                   isWeekend ? Colors.Grey.Lighten3 : Colors.White)
                        .Padding(5).Column(cellColumn =>
                        {
                            var dayText = cellColumn.Item().AlignRight().Text(currentDay.ToString())
                                .FontSize(14);
                            
                            if (isWorked)
                                dayText.Bold();

                            if (isWorked)
                            {
                                cellColumn.Item().AlignCenter().PaddingTop(10)
                                    .Text("✓").FontSize(20)
                                    .FontColor(Colors.Green.Darken3).Bold();
                            }
                        });

                    currentDay++;
                }
            }
        });
    }

    private void ComposeLegend(IContainer container)
    {
        container.Row(row =>
        {
            row.Spacing(20);

            row.AutoItem().Row(legendRow =>
            {
                legendRow.ConstantItem(20).Height(20).Background(Colors.Green.Lighten3)
                    .Border(1).BorderColor(Colors.Grey.Medium);
                legendRow.AutoItem().PaddingLeft(5).Text("Worked Day");
            });

            row.AutoItem().Row(legendRow =>
            {
                legendRow.ConstantItem(20).Height(20).Background(Colors.Grey.Lighten3)
                    .Border(1).BorderColor(Colors.Grey.Medium);
                legendRow.AutoItem().PaddingLeft(5).Text("Weekend");
            });

            row.AutoItem().Row(legendRow =>
            {
                legendRow.ConstantItem(20).Height(20).Background(Colors.White)
                    .Border(1).BorderColor(Colors.Grey.Medium);
                legendRow.AutoItem().PaddingLeft(5).Text("Non-Working Day");
            });
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

    private static string GetMonthName(int month)
    {
        return new DateTime(2000, month, 1).ToString("MMMM");
    }
}
