# HTML Template System - Complete Implementation Guide

## ✅ Phase 4 Complete: Backend Integration

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│  Frontend (Vue)                                              │
│  └─ TemplateForm: HTML editor with validation               │
└────────────────────┬────────────────────────────────────────┘
                     │ POST /api/invoicetemplates
                     ↓
┌─────────────────────────────────────────────────────────────┐
│  API Layer (ASP.NET Core)                                    │
│  ├─ InvoiceTemplatesController                               │
│  └─ ValidateTemplateCommand                                  │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────┐
│  Application Layer                                           │
│  ├─ ITemplateRenderer (Scriban)                              │
│  ├─ IHtmlToPdfConverter (QuestPDF)                           │
│  └─ InvoiceGenerationService                                 │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────┐
│  Domain/Data Layer                                           │
│  └─ InvoiceTemplate Entity (stores HTML)                    │
└─────────────────────────────────────────────────────────────┘
```

## Two-Stage Rendering Process

### Stage 1: Template Rendering (Scriban)

**Input:** Template with Scriban syntax
```html
<h1>Invoice #{{ invoiceNumber }}</h1>
<p>Customer: {{ customer.name }}</p>
{{ for item in lineItems }}
<tr><td>{{ item.description }}</td></tr>
{{ end }}
```

**Process:**
1. Load InvoiceTemplate from database
2. Build InvoiceTemplateModel from Invoice entity
3. Call `ITemplateRenderer.RenderAsync(template, model)`
4. Scriban evaluates placeholders, loops, conditionals

**Output:** Pure HTML
```html
<h1>Invoice #INV-2024-01-001</h1>
<p>Customer: Acme Corp</p>
<tr><td>Professional Services</td></tr>
<tr><td>Expense: Travel</td></tr>
```

### Stage 2: PDF Generation (QuestPDF)

**Input:** Rendered HTML
**Process:**
1. Parse HTML with HtmlAgilityPack
2. Map HTML elements to QuestPDF components:
   - `<h1>` → `Text().FontSize(24).Bold()`
   - `<table>` → `Table()` with columns
   - `<tr>` → `Cell()` with borders
3. Generate PDF document

**Output:** PDF bytes (byte[])

## Service Integration

### InvoiceGenerationService Methods

#### 1. GenerateInvoiceAsync()

Creates invoice and renders HTML template.

```csharp
public async Task<Invoice> GenerateInvoiceAsync(
    GenerateInvoiceDto dto,
    CancellationToken cancellationToken)
{
    // 1. Load dependencies
    var template = await GetTemplateAsync(...);
    var customer = await _customerRepository.GetByIdAsync(...);
    var rate = await GetRateAsync(...);
    var taxes = await GetCustomerTaxesAsync(...);
    
    // 2. Calculate amounts
    var subtotal = CalculateSubtotal(dto, rate);
    var (totalTax, taxLines) = _taxCalculationService.CalculateTaxes(...);
    
    // 3. Create Invoice entity
    var invoice = new Invoice(...);
    
    // 4. Build template model
    var templateModel = BuildTemplateModel(invoice, customer, dto, rate);
    
    // 5. Render template
    var html = await _templateRenderer.RenderAsync(
        template.Content, 
        templateModel, 
        cancellationToken);
    
    // 6. Store rendered HTML
    invoice.SetRenderedContent(html);
    
    return invoice;
}
```

#### 2. GenerateInvoicePdfAsync()

Converts stored HTML to PDF.

```csharp
public async Task<byte[]> GenerateInvoicePdfAsync(
    long invoiceId,
    CancellationToken cancellationToken)
{
    // 1. Load invoice with rendered HTML
    var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, ...);
    
    if (string.IsNullOrEmpty(invoice.RenderedContent))
        throw new InvalidOperationException("No rendered content");
    
    // 2. Convert HTML to PDF
    var pdfBytes = await _htmlToPdfConverter.ConvertAsync(
        invoice.RenderedContent, 
        cancellationToken);
    
    return pdfBytes;
}
```

### Template Model Structure

```csharp
public sealed record InvoiceTemplateModel
{
    // Invoice metadata
    public string InvoiceNumber { get; init; }
    public string InvoiceType { get; init; }
    public string Date { get; init; }
    public string Currency { get; init; }
    
    // Customer data
    public CustomerTemplateModel Customer { get; init; }
    
    // Line items (services + expenses)
    public List<LineItemTemplateModel> LineItems { get; init; }
    
    // Taxes
    public List<TaxTemplateModel> Taxes { get; init; }
    
    // Amounts
    public decimal Subtotal { get; init; }
    public decimal TotalTax { get; init; }
    public decimal Total { get; init; }
    
    // Monthly invoice specific
    public int? WorkedDays { get; init; }
    public int? MonthNumber { get; init; }
    public string? MonthDescription { get; init; }
    public decimal? MonthlyRate { get; init; }
    public decimal? TotalExpenses { get; init; }
}
```

## Template Syntax Reference

### Basic Placeholders

Access any property using dot notation:

```html
{{ invoiceNumber }}
{{ customer.name }}
{{ customer.address.city }}
{{ subtotal }}
{{ total }}
{{ currency }}
```

### Custom Functions

```html
<!-- Format currency with symbol -->
{{ FormatCurrency total currency }}
<!-- Output: "1,234.56 EUR" -->

<!-- Format date -->
{{ FormatDate date "MMMM dd, yyyy" }}
<!-- Output: "January 23, 2024" -->

<!-- Format decimal -->
{{ FormatDecimal rate 2 }}
<!-- Output: "123.46" -->
```

### Loops

```html
{{ for item in lineItems }}
<tr>
  <td>{{ item.description }}</td>
  <td>{{ item.quantity }}</td>
  <td>{{ FormatCurrency item.amount currency }}</td>
</tr>
{{ end }}
```

### Conditionals

```html
{{ if workedDays }}
<p>Days Worked: {{ workedDays }}</p>
<p>Monthly Rate: {{ FormatCurrency monthlyRate currency }}</p>
{{ end }}

{{ if totalExpenses }}
<tr>
  <td>Total Expenses</td>
  <td>{{ FormatCurrency totalExpenses currency }}</td>
</tr>
{{ end }}
```

### Nested Loops

```html
{{ for item in lineItems }}
<div class="line-item">
  <h4>{{ item.description }}</h4>
  
  {{ if item.details }}
  <ul>
    {{ for detail in item.details }}
    <li>{{ detail }}</li>
    {{ end }}
  </ul>
  {{ end }}
</div>
{{ end }}
```

## HTML Support

### Supported Tags

- **Headings:** `<h1>` - `<h6>`
- **Paragraphs:** `<p>`, `<div>`
- **Tables:** `<table>`, `<tr>`, `<td>`, `<th>`
- **Formatting:** `<strong>`, `<em>`, `<br>`
- **Containers:** `<html>`, `<body>`, `<head>`

### Table Attributes

```html
<table>
  <tr>
    <th width="60%">Description</th>
    <th width="20%">Quantity</th>
    <th width="20%">Amount</th>
  </tr>
</table>
```

Width attribute sets relative column width (as percentage).

## Example Templates

### Monthly Invoice with Expenses

```html
<!DOCTYPE html>
<html>
<body>
  <h1>INVOICE</h1>
  <h3>Invoice #{{ invoiceNumber }}</h3>
  <p>Date: {{ date }}</p>
  
  <h3>Bill To:</h3>
  <p>
    <strong>{{ customer.name }}</strong><br>
    {{ customer.address.street }}<br>
    {{ customer.address.city }}, {{ customer.address.postalCode }}<br>
    VAT: {{ customer.fiscalId }}
  </p>
  
  <h3>Services</h3>
  <table>
    <tr>
      <th width="60%">Description</th>
      <th width="20%">Days</th>
      <th width="20%">Amount</th>
    </tr>
    
    <tr>
      <td>Professional Services - {{ monthDescription }}</td>
      <td>{{ workedDays }}</td>
      <td>{{ FormatCurrency subtotal currency }}</td>
    </tr>
    
    {{ if totalExpenses }}
    {{ for item in lineItems }}
    {{ if item.description contains "Expense" }}
    <tr>
      <td>{{ item.description }}</td>
      <td>1</td>
      <td>{{ FormatCurrency item.amount currency }}</td>
    </tr>
    {{ end }}
    {{ end }}
    {{ end }}
    
    <tr>
      <td><strong>Subtotal</strong></td>
      <td></td>
      <td><strong>{{ FormatCurrency subtotal currency }}</strong></td>
    </tr>
    
    {{ for tax in taxes }}
    <tr>
      <td>{{ tax.description }} ({{ FormatDecimal tax.rate 2 }}%)</td>
      <td></td>
      <td>{{ FormatCurrency tax.amount currency }}</td>
    </tr>
    {{ end }}
    
    <tr>
      <td><h3>TOTAL</h3></td>
      <td></td>
      <td><h3>{{ FormatCurrency total currency }}</h3></td>
    </tr>
  </table>
  
  <p>Thank you for your business!</p>
</body>
</html>
```

## Testing

### Unit Tests (ScribanTemplateRendererTests)

```csharp
[Test]
public async Task RenderAsync_WithLoop_ReturnsRenderedList()
{
    var template = "{{ for item in items }}{{ item.name }}{{ end }}";
    var model = new { Items = new[] { 
        new { Name = "Apple" }, 
        new { Name = "Banana" } 
    }};
    
    var result = await _renderer.RenderAsync(template, model);
    
    result.ShouldContain("Apple");
    result.ShouldContain("Banana");
}
```

### Integration Testing (Phase 6)

1. Create invoice with template
2. Generate invoice (renders HTML)
3. Generate PDF
4. Verify PDF content

## API Usage

### Validate Template

```http
POST /api/invoicetemplates/validate
Content-Type: application/json

{
  "templateContent": "<h1>{{ invoiceNumber }}</h1>"
}
```

Response:
```json
{
  "isValid": true,
  "errors": [],
  "placeholders": ["invoiceNumber"]
}
```

### Create Template

```http
POST /api/invoicetemplates
Content-Type: application/json

{
  "customerId": 1,
  "invoiceType": 0,
  "content": "<!DOCTYPE html>..."
}
```

### Generate Invoice

```http
POST /api/invoices/generate
Content-Type: application/json

{
  "customerId": 1,
  "invoiceType": 0,
  "issueDate": "2024-01-23",
  "year": 2024,
  "month": 1,
  "workDays": [1, 2, 3, 4, 5]
}
```

## Next Steps: Frontend (Phase 5)

See separate document: `frontend-implementation-guide.md`

## Troubleshooting

### Template doesn't render

Check:
1. Template syntax is valid (use validate endpoint)
2. All placeholders exist in InvoiceTemplateModel
3. Scriban syntax is correct (case-sensitive)

### PDF generation fails

Check:
1. HTML is valid
2. All HTML tags are supported
3. Table structure is correct (matching <tr> and <td> counts)

### Missing data in rendered output

Check:
1. InvoiceTemplateModel includes all required data
2. Property names match exactly (case-sensitive)
3. Null values are handled in template

## Performance Considerations

1. **Template Caching:** Templates are loaded per request (consider caching)
2. **PDF Generation:** QuestPDF is fast but CPU-intensive
3. **HTML Parsing:** HtmlAgilityPack is efficient for simple HTML
4. **Async Operations:** All I/O operations are async

## Security Considerations

1. **Template Validation:** Always validate before saving
2. **No Code Execution:** Scriban doesn't allow arbitrary code
3. **XSS Protection:** HTML is generated server-side
4. **Input Sanitization:** Scriban auto-escapes HTML entities

---

**Implementation Status:** Backend 100% Complete ✅
**Next Phase:** Frontend Integration 🚧
