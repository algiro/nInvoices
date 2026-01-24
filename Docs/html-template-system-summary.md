# HTML Template System - Implementation Summary

## ✅ What's Completed

### Backend Infrastructure
1. **Scriban Template Renderer** (ITemplateRenderer)
   - Custom functions: FormatCurrency, FormatDate, FormatDecimal
   - Automatic PascalCase → camelCase conversion
   - Full Scriban/Liquid syntax support
   - 13/13 unit tests passing

2. **QuestPDF HTML Converter** (IHtmlToPdfConverter)
   - Converts simple HTML to PDF using QuestPDF
   - Supports: h1-h6, p, div, table, tr, td, th, br
   - Table column widths via width="X%" attribute
   - Lightweight, fast, no external dependencies

3. **Template Models**
   - InvoiceTemplateModel (main model)
   - CustomerTemplateModel
   - AddressTemplateModel
   - LineItemTemplateModel
   - TaxTemplateModel

4. **API Endpoints**
   - POST /api/invoicetemplates/validate - Validates template syntax
   - Returns: isValid, errors[], placeholders[]

## 📝 Template Syntax

### Available Placeholders
- `{{ invoiceNumber }}` - Invoice number
- `{{ date }}` - Invoice date
- `{{ currency }}` - Currency code (EUR, USD, GBP)
- `{{ customer.name }}` - Customer name
- `{{ customer.fiscalId }}` - VAT/Tax ID
- `{{ customer.address.street }}` - Address components
- `{{ subtotal }}` - Subtotal amount
- `{{ total }}` - Total amount with taxes
- `{{ workedDays }}` - Days worked (monthly invoices)
- `{{ monthDescription }}` - Month name

### Custom Functions
- `{{ FormatCurrency amount currency }}` → "1,234.56 EUR"
- `{{ FormatDate date "yyyy-MM-dd" }}` → "2024-01-23"
- `{{ FormatDecimal value 2 }}` → "123.46"

### Loops
```html
{{ for item in lineItems }}
<tr>
  <td>{{ item.description }}</td>
  <td>{{ FormatCurrency item.amount currency }}</td>
</tr>
{{ end }}
```

### Conditionals
```html
{{ if workedDays }}
<p>Days Worked: {{ workedDays }}</p>
{{ end }}
```

## 🔄 Two-Stage Rendering Flow

```
User Template (HTML + Scriban syntax)
         ↓
   Scriban Renderer
   - Expands {{ placeholders }}
   - Evaluates {{ for }} loops
   - Processes {{ if }} conditions
         ↓
   Pure HTML (rendered)
         ↓
   QuestPDF HTML Converter
   - Parses HTML structure
   - Maps to QuestPDF components
         ↓
   PDF Document (bytes)
```

## 📦 Dependencies Added
- Scriban 6.5.2 (Template rendering)
- HtmlAgilityPack 1.12.4 (HTML parsing)

## 🎯 Next Steps

### Immediate (Phase 4 - Service Integration)
1. Update InvoiceGenerationService to use new flow:
   ```csharp
   template → ITemplateRenderer → HTML → IHtmlToPdfConverter → PDF
   ```

2. Add expenses list to InvoiceTemplateModel

3. Test with real invoice data

### Frontend (Phase 5)
1. Replace current template editor with HTML textarea
2. Add "Placeholder Reference" collapsible panel
3. Add "Sample Templates" dropdown
4. Add "Validate" button (calls /api/invoicetemplates/validate)
5. Add "Preview" button (renders with test data)

### Polish (Phase 6)
1. Add more HTML tags support (ul, ol, li, hr)
2. Add CSS style parsing (colors, fonts, borders)
3. Add template export/import
4. Add template versioning UI

## 📖 Example Template

See: `src/nInvoices.Application/Templates/DefaultInvoiceTemplate.html`

Key features demonstrated:
- Customer information block
- Table with dynamic line items
- Conditional expenses section
- Tax breakdown
- Monthly invoice metadata

## 🧪 Testing

### Unit Tests
- Location: `tests/nInvoices.Application.Tests/Services/ScribanTemplateRendererTests.cs`
- Coverage: 13 tests, all passing
- Tests: placeholders, loops, conditionals, functions, validation

### Integration Testing (TODO)
- Generate invoice with custom template
- Verify PDF output
- Test error handling
