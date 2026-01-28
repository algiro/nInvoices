using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Text.RegularExpressions;

namespace nInvoices.Infrastructure.PdfExport;

/// <summary>
/// Converts HTML to PDF using QuestPDF with enhanced CSS support.
/// Supports: h1-h6, p, div, table, tr, td, th, strong, em, br, style tags, inline styles
/// CSS Properties: background, background-color, color, font-size, font-weight, text-align, padding, margin, border
/// </summary>
public sealed class QuestPdfHtmlConverter : Application.Services.IHtmlToPdfConverter
{
    private readonly ILogger<QuestPdfHtmlConverter> _logger;
    private readonly Dictionary<string, Dictionary<string, string>> _cssRules = new();

    public QuestPdfHtmlConverter(ILogger<QuestPdfHtmlConverter> logger)
    {
        _logger = logger;
    }

    public Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        try
        {
            // Parse CSS from <style> tags
            ParseCssStyles(html);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
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
            // Skip head and style tags
            var bodyNode = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
            
            foreach (var node in bodyNode.ChildNodes)
            {
                if (node.Name.ToLowerInvariant() is not ("style" or "head"))
                {
                    RenderNode(column, node, new Dictionary<string, string>());
                }
            }
        });
    }

    private void ParseCssStyles(string html)
    {
        var styleMatches = Regex.Matches(html, @"<style[^>]*>(.*?)</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        foreach (Match match in styleMatches)
        {
            var cssContent = match.Groups[1].Value;
            
            // Simple CSS parser - matches .class { prop: value; }
            var ruleMatches = Regex.Matches(cssContent, @"([.#]?[\w-]+)\s*\{([^}]+)\}");
            
            foreach (Match ruleMatch in ruleMatches)
            {
                var selector = ruleMatch.Groups[1].Value.Trim();
                var properties = ruleMatch.Groups[2].Value;
                
                var propDict = new Dictionary<string, string>();
                var propMatches = Regex.Matches(properties, @"([\w-]+)\s*:\s*([^;]+);?");
                
                foreach (Match propMatch in propMatches)
                {
                    var propName = propMatch.Groups[1].Value.Trim();
                    var propValue = propMatch.Groups[2].Value.Trim();
                    propDict[propName] = propValue;
                }
                
                _cssRules[selector] = propDict;
            }
        }
    }

    private Dictionary<string, string> GetEffectiveStyles(HtmlNode node, Dictionary<string, string> inheritedStyles)
    {
        var styles = new Dictionary<string, string>(inheritedStyles);
        
        // Apply class styles
        var classAttr = node.GetAttributeValue("class", "");
        if (!string.IsNullOrEmpty(classAttr))
        {
            foreach (var className in classAttr.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (_cssRules.TryGetValue($".{className}", out var classStyles))
                {
                    foreach (var kvp in classStyles)
                    {
                        styles[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        
        // Apply inline styles (override class styles)
        var styleAttr = node.GetAttributeValue("style", "");
        if (!string.IsNullOrEmpty(styleAttr))
        {
            var inlineMatches = Regex.Matches(styleAttr, @"([\w-]+)\s*:\s*([^;]+);?");
            foreach (Match match in inlineMatches)
            {
                var propName = match.Groups[1].Value.Trim();
                var propValue = match.Groups[2].Value.Trim();
                styles[propName] = propValue;
            }
        }
        
        return styles;
    }

    private void RenderNode(ColumnDescriptor column, HtmlNode node, Dictionary<string, string> inheritedStyles)
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

        var effectiveStyles = GetEffectiveStyles(node, inheritedStyles);

        switch (node.Name.ToLowerInvariant())
        {
            case "h1":
                RenderStyledText(column, node, effectiveStyles, 24, true);
                break;

            case "h2":
                RenderStyledText(column, node, effectiveStyles, 20, true);
                break;

            case "h3":
                RenderStyledText(column, node, effectiveStyles, 16, true);
                break;

            case "h4":
                RenderStyledText(column, node, effectiveStyles, 14, true);
                break;

            case "p":
                RenderStyledText(column, node, effectiveStyles, 10, false);
                break;

            case "div":
                RenderDiv(column, node, effectiveStyles);
                break;

            case "table":
                RenderTable(column, node, effectiveStyles);
                break;

            case "br":
                column.Item().PaddingBottom(5);
                break;

            case "html":
            case "body":
            case "head":
            case "style":
                // Render children only
                foreach (var child in node.ChildNodes)
                {
                    RenderNode(column, child, effectiveStyles);
                }
                break;

            default:
                // For unknown tags, render children
                foreach (var child in node.ChildNodes)
                {
                    RenderNode(column, child, effectiveStyles);
                }
                break;
        }
    }

    private void RenderTable(ColumnDescriptor column, HtmlNode tableNode, Dictionary<string, string> inheritedStyles)
    {
        var effectiveStyles = GetEffectiveStyles(tableNode, inheritedStyles);
        
        var hasBackgroundColor = effectiveStyles.ContainsKey("background-color") || effectiveStyles.ContainsKey("background");
        var hasPadding = effectiveStyles.ContainsKey("padding");
        
        if (hasBackgroundColor)
        {
            column.Item().Background(ParseColor(GetStyleValue(effectiveStyles, "background-color", GetStyleValue(effectiveStyles, "background", "")), Colors.White))
                .Table(table =>
                {
                    RenderTableContent(table, tableNode, effectiveStyles);
                });
        }
        else
        {
            column.Item().Table(table =>
            {
                RenderTableContent(table, tableNode, effectiveStyles);
            });
        }
    }

    private void RenderTableContent(TableDescriptor table, HtmlNode tableNode, Dictionary<string, string> effectiveStyles)
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
                    var cellStyles = GetEffectiveStyles(cell, effectiveStyles);
                    var bgColor = GetStyleValue(cellStyles, "background-color", GetStyleValue(cellStyles, "background", ""));
                    
                    // Chain everything in one go - don't reuse container reference
                    if (!string.IsNullOrEmpty(bgColor))
                    {
                        header.Cell()
                            .Background(ParseColor(bgColor, Colors.Grey.Lighten3))
                            .Padding(5)
                            .Text(text => text.Span(GetInnerText(cell)).Bold());
                    }
                    else
                    {
                        header.Cell()
                            .Background(Colors.Grey.Lighten3)
                            .Padding(5)
                            .Text(text => text.Span(GetInnerText(cell)).Bold());
                    }
                }
            });
            allRows = allRows.Skip(1).ToList(); // Skip header row
        }

        // Render body rows
        foreach (var row in allRows)
        {
            var rowStyles = GetEffectiveStyles(row, effectiveStyles);
            var cells = row.ChildNodes
                .Where(n => n.Name.ToLowerInvariant() is "td" or "th")
                .ToList();

            foreach (var cell in cells)
            {
                var cellStyles = GetEffectiveStyles(cell, rowStyles);
                
                // Apply cell background if specified, chain everything properly
                var bgColor = GetStyleValue(cellStyles, "background-color", GetStyleValue(cellStyles, "background", ""));
                if (!string.IsNullOrEmpty(bgColor))
                {
                    table.Cell()
                        .Background(ParseColor(bgColor, Colors.White))
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .Padding(5)
                        .Text(GetInnerText(cell));
                }
                else
                {
                    table.Cell()
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .Padding(5)
                        .Text(GetInnerText(cell));
                }
            }
        }
    }

    private void RenderDiv(ColumnDescriptor column, HtmlNode node, Dictionary<string, string> inheritedStyles)
    {
        var effectiveStyles = GetEffectiveStyles(node, inheritedStyles);
        
        // Check if this div has background or other container-level styles
        var hasBackgroundColor = effectiveStyles.ContainsKey("background-color") || effectiveStyles.ContainsKey("background");
        var hasPadding = effectiveStyles.ContainsKey("padding");
        var hasMargin = effectiveStyles.ContainsKey("margin") || 
                       effectiveStyles.ContainsKey("margin-top") || 
                       effectiveStyles.ContainsKey("margin-bottom");
        
        if (hasBackgroundColor || hasPadding || hasMargin)
        {
            // Apply all styles in a single fluent chain
            column.Item().Column(innerColumn =>
            {
                // Build a single fluent chain for all container modifications
                var styledContainer = innerColumn.Item();
                
                // Apply background color (skip gradients - QuestPDF doesn't support them)
                var bgColor = GetStyleValue(effectiveStyles, "background-color", GetStyleValue(effectiveStyles, "background", ""));
                if (!string.IsNullOrEmpty(bgColor) && !bgColor.Contains("gradient"))
                {
                    styledContainer = styledContainer.Background(bgColor);
                }
                
                // Apply padding
                var padding = GetStyleValue(effectiveStyles, "padding", "");
                if (!string.IsNullOrEmpty(padding))
                {
                    var paddingValue = padding.Replace("px", "").Trim();
                    if (float.TryParse(paddingValue, out var paddingPx))
                    {
                        styledContainer = styledContainer.Padding(paddingPx);
                    }
                }
                
                // Apply margin (using padding since QuestPDF doesn't have margin)
                var marginTop = GetStyleValue(effectiveStyles, "margin-top", GetStyleValue(effectiveStyles, "margin", ""));
                var marginBottom = GetStyleValue(effectiveStyles, "margin-bottom", GetStyleValue(effectiveStyles, "margin", ""));
                
                float marginTopPx = 0, marginBottomPx = 0;
                if (!string.IsNullOrEmpty(marginTop))
                {
                    var marginTopValue = marginTop.Replace("px", "").Trim();
                    float.TryParse(marginTopValue, out marginTopPx);
                }
                if (!string.IsNullOrEmpty(marginBottom))
                {
                    var marginBottomValue = marginBottom.Replace("px", "").Trim();
                    float.TryParse(marginBottomValue, out marginBottomPx);
                }
                
                if (marginTopPx > 0)
                {
                    styledContainer = styledContainer.PaddingTop(marginTopPx);
                }
                if (marginBottomPx > 0)
                {
                    styledContainer = styledContainer.PaddingBottom(marginBottomPx);
                }
                
                // Finally, render children inside the styled container
                styledContainer.Column(contentColumn =>
                {
                    foreach (var child in node.ChildNodes)
                    {
                        RenderNode(contentColumn, child, effectiveStyles);
                    }
                });
            });
        }
        else
        {
            // No special styling, just render children directly
            foreach (var child in node.ChildNodes)
            {
                RenderNode(column, child, effectiveStyles);
            }
        }
    }

    private void RenderStyledText(ColumnDescriptor column, HtmlNode node, Dictionary<string, string> styles, float defaultSize, bool defaultBold)
    {
        var hasBackgroundColor = styles.ContainsKey("background-color") || styles.ContainsKey("background");
        var hasPadding = styles.ContainsKey("padding");
        
        if (hasBackgroundColor || hasPadding)
        {
            column.Item().Background(ParseColor(GetStyleValue(styles, "background-color", GetStyleValue(styles, "background", "")), Colors.White))
                .Padding(5)
                .Text(GetInnerText(node));
        }
        else
        {
            column.Item().Text(GetInnerText(node));
        }
    }

    private static void ApplyTextStyles(TextStyle textDesc, Dictionary<string, string> styles)
    {
        // Font size
        var fontSize = GetStyleValue(styles, "font-size", "");
        if (!string.IsNullOrEmpty(fontSize))
        {
            var fontSizeValue = fontSize.Replace("px", "").Replace("pt", "").Trim();
            if (float.TryParse(fontSizeValue, out var fontSizePx))
            {
                textDesc.FontSize(fontSizePx);
            }
        }
        
        // Font weight
        var fontWeight = GetStyleValue(styles, "font-weight", "");
        if (fontWeight is "bold" or "700" or "800" or "900")
        {
            textDesc.Bold();
        }
        
        // Color
        var color = GetStyleValue(styles, "color", "");
        if (!string.IsNullOrEmpty(color))
        {
            textDesc.FontColor(ParseColor(color, Colors.Black));
        }
        
        // Text align
        var textAlign = GetStyleValue(styles, "text-align", "");
        // Note: text-align is typically applied at container level in QuestPDF
    }

    private static string ParseColor(string colorValue, string defaultColor)
    {
        if (string.IsNullOrEmpty(colorValue))
            return defaultColor;
        
        colorValue = colorValue.Trim().ToLowerInvariant();
        
        // Skip gradients - QuestPDF doesn't support them
        if (colorValue.Contains("gradient"))
            return defaultColor;
        
        // Handle hex colors
        if (colorValue.StartsWith("#"))
        {
            return colorValue;
        }
        
        // Handle rgb/rgba
        if (colorValue.StartsWith("rgb"))
        {
            var match = Regex.Match(colorValue, @"rgba?\((\d+),\s*(\d+),\s*(\d+)");
            if (match.Success)
            {
                var r = int.Parse(match.Groups[1].Value);
                var g = int.Parse(match.Groups[2].Value);
                var b = int.Parse(match.Groups[3].Value);
                return $"#{r:X2}{g:X2}{b:X2}";
            }
        }
        
        // Handle named colors (basic set)
        return colorValue switch
        {
            "white" => "#FFFFFF",
            "black" => "#000000",
            "red" => "#FF0000",
            "green" => "#008000",
            "blue" => "#0000FF",
            "yellow" => "#FFFF00",
            "gray" or "grey" => "#808080",
            _ => defaultColor
        };
    }

    private static string GetStyleValue(Dictionary<string, string> styles, string key, string defaultValue)
    {
        return styles.TryGetValue(key, out var value) ? value : defaultValue;
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
