using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace nInvoices.Infrastructure.PdfExport;

/// <summary>
/// Converts simple HTML to PDF using QuestPDF.
/// Supports: h1-h6, p, div, table, tr, td, th, strong, em, br
/// Attributes: width (for columns), align, style (limited)
/// </summary>
public sealed class QuestPdfHtmlConverter : Application.Services.IHtmlToPdfConverter
{
    private readonly ILogger<QuestPdfHtmlConverter> _logger;

    public QuestPdfHtmlConverter(ILogger<QuestPdfHtmlConverter> logger)
    {
        _logger = logger;
    }

    public Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        try
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Content().Element(content =>
                    {
                        RenderHtml(content, html);
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            });

            var pdfBytes = document.GeneratePdf();
            return Task.FromResult(pdfBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert HTML to PDF");
            throw new InvalidOperationException("PDF generation failed", ex);
        }
    }

    private void RenderHtml(IContainer container, string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        container.Column(column =>
        {
            foreach (var node in doc.DocumentNode.ChildNodes)
            {
                RenderNode(column, node);
            }
        });
    }

    private void RenderNode(ColumnDescriptor column, HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = node.InnerText.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                column.Item().Text(text);
            }
            return;
        }

        if (node.NodeType != HtmlNodeType.Element)
            return;

        switch (node.Name.ToLowerInvariant())
        {
            case "h1":
                column.Item().Text(node.InnerText).FontSize(24).Bold();
                break;

            case "h2":
                column.Item().Text(node.InnerText).FontSize(20).Bold();
                break;

            case "h3":
                column.Item().Text(node.InnerText).FontSize(16).Bold();
                break;

            case "h4":
                column.Item().Text(node.InnerText).FontSize(14).Bold();
                break;

            case "p":
                column.Item().PaddingBottom(5).Text(GetInnerText(node));
                break;

            case "div":
                column.Item().Column(innerColumn =>
                {
                    foreach (var child in node.ChildNodes)
                    {
                        RenderNode(innerColumn, child);
                    }
                });
                break;

            case "table":
                RenderTable(column, node);
                break;

            case "br":
                column.Item().PaddingBottom(5);
                break;

            case "html":
            case "body":
                // Render children
                foreach (var child in node.ChildNodes)
                {
                    RenderNode(column, child);
                }
                break;

            default:
                _logger.LogWarning("Unsupported HTML tag: {TagName}", node.Name);
                break;
        }
    }

    private void RenderTable(ColumnDescriptor column, HtmlNode tableNode)
    {
        column.Item().Table(table =>
        {
            // Find all rows (including in thead/tbody)
            var allRows = tableNode.Descendants("tr").ToList();
            if (!allRows.Any())
                return;

            // Determine column count from first row
            var firstRow = allRows.First();
            var columnCount = firstRow.ChildNodes
                .Count(n => n.Name.ToLowerInvariant() is "th" or "td");

            // Define columns with relative widths - MUST be called only once
            var headerCells = firstRow.ChildNodes
                .Where(n => n.Name.ToLowerInvariant() is "th" or "td")
                .ToList();

            table.ColumnsDefinition(columns =>
            {
                for (int i = 0; i < columnCount; i++)
                {
                    var width = GetColumnWidth(headerCells.ElementAtOrDefault(i));
                    if (width > 0)
                        columns.RelativeColumn(width);
                    else
                        columns.RelativeColumn(); // Equal width
                }
            });

            // Render header row if first row contains <th>
            if (firstRow.ChildNodes.Any(n => n.Name.ToLowerInvariant() == "th"))
            {
                table.Header(header =>
                {
                    foreach (var cell in firstRow.ChildNodes.Where(n => n.Name.ToLowerInvariant() is "th" or "td"))
                    {
                        header.Cell()
                            .Background(Colors.Grey.Lighten3)
                            .Padding(5)
                            .Text(GetInnerText(cell))
                            .Bold();
                    }
                });
                allRows = allRows.Skip(1).ToList(); // Skip header row
            }

            // Render body rows
            foreach (var row in allRows)
            {
                var cells = row.ChildNodes
                    .Where(n => n.Name.ToLowerInvariant() is "td" or "th")
                    .ToList();

                foreach (var cell in cells)
                {
                    table.Cell()
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .Padding(5)
                        .Text(GetInnerText(cell));
                }
            }
        });
    }

    private static float GetColumnWidth(HtmlNode? cell)
    {
        if (cell == null)
            return 0;

        var widthAttr = cell.GetAttributeValue("width", "");
        if (string.IsNullOrEmpty(widthAttr))
            return 0;

        // Parse percentage (e.g., "80%")
        if (widthAttr.EndsWith("%"))
        {
            if (float.TryParse(widthAttr.TrimEnd('%'), out var percentage))
                return percentage;
        }

        return 0;
    }

    private static string GetInnerText(HtmlNode node)
    {
        // Decode HTML entities and trim
        return HtmlEntity.DeEntitize(node.InnerText).Trim();
    }
}
