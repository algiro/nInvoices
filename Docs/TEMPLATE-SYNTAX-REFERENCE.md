# Template Syntax - Quick Reference Card

## 📝 Placeholders (camelCase)

### Invoice Information
| Placeholder | Description | Example Value |
|-------------|-------------|---------------|
| `{{ invoiceNumber }}` | Invoice number | INV-2024-01-001 |
| `{{ invoiceType }}` | Invoice type | Monthly/OneTime |
| `{{ date }}` | Invoice date | 2024-01-23 |
| `{{ dueDate }}` | Due date | 2024-02-23 |
| `{{ currency }}` | Currency code | EUR, USD, GBP |

### Customer Information
| Placeholder | Description |
|-------------|-------------|
| `{{ customer.name }}` | Customer name |
| `{{ customer.fiscalId }}` | VAT/Tax ID |
| `{{ customer.address.street }}` | Street address |
| `{{ customer.address.city }}` | City |
| `{{ customer.address.postalCode }}` | Postal/ZIP code |
| `{{ customer.address.country }}` | Country |

### Amounts
| Placeholder | Description |
|-------------|-------------|
| `{{ subtotal }}` | Subtotal (before tax) |
| `{{ totalTax }}` | Total tax amount |
| `{{ total }}` | Final total |

### Monthly Invoice Only
| Placeholder | Description |
|-------------|-------------|
| `{{ workedDays }}` | Number of days worked |
| `{{ monthNumber }}` | Month (1-12) |
| `{{ monthDescription }}` | Month name (e.g., "January") |
| `{{ monthlyRate }}` | Daily rate × worked days |
| `{{ totalExpenses }}` | Total expenses |

## �� Loops

### Line Items
```html
{{ for item in lineItems }}
  <tr>
    <td>{{ item.description }}</td>
    <td>{{ item.quantity }}</td>
    <td>{{ item.rate }}</td>
    <td>{{ FormatCurrency item.amount currency }}</td>
  </tr>
{{ end }}
```

**Available properties:**
- `item.description` - Description
- `item.quantity` - Quantity
- `item.rate` - Unit rate
- `item.amount` - Total amount

### Taxes
```html
{{ for tax in taxes }}
  <tr>
    <td>{{ tax.description }}</td>
    <td>{{ FormatDecimal tax.rate 2 }}%</td>
    <td>{{ FormatCurrency tax.amount currency }}</td>
  </tr>
{{ end }}
```

**Available properties:**
- `tax.description` - Tax description
- `tax.rate` - Tax rate percentage
- `tax.amount` - Tax amount

## ❓ Conditionals

### Basic If
```html
{{ if workedDays }}
  <p>Days Worked: {{ workedDays }}</p>
{{ end }}
```

### If-Else
```html
{{ if totalExpenses }}
  <p>Expenses: {{ FormatCurrency totalExpenses currency }}</p>
{{ else }}
  <p>No expenses</p>
{{ end }}
```

### Check for Existence
```html
{{ if dueDate }}
  <p>Due Date: {{ dueDate }}</p>
{{ end }}
```

## 🔧 Custom Functions

### FormatCurrency
**Format:** `{{ FormatCurrency amount currency }}`
**Output:** `1,234.56 EUR`

```html
{{ FormatCurrency total currency }}
{{ FormatCurrency subtotal currency }}
{{ FormatCurrency item.amount currency }}
```

### FormatDate
**Format:** `{{ FormatDate date "format-string" }}`

**Common formats:**
- `"yyyy-MM-dd"` → 2024-01-23
- `"MMMM dd, yyyy"` → January 23, 2024
- `"dd/MM/yyyy"` → 23/01/2024
- `"MMM yyyy"` → Jan 2024

```html
{{ FormatDate date "MMMM dd, yyyy" }}
```

### FormatDecimal
**Format:** `{{ FormatDecimal value decimals }}`
**Output:** `123.46`

```html
{{ FormatDecimal tax.rate 2 }}%
{{ FormatDecimal total 2 }}
```

## 📊 HTML Tables

### Basic Table
```html
<table>
  <tr>
    <th>Column 1</th>
    <th>Column 2</th>
  </tr>
  <tr>
    <td>Data 1</td>
    <td>Data 2</td>
  </tr>
</table>
```

### Table with Column Widths
```html
<table>
  <tr>
    <th width="60%">Description</th>
    <th width="20%">Quantity</th>
    <th width="20%">Amount</th>
  </tr>
  {{ for item in lineItems }}
  <tr>
    <td>{{ item.description }}</td>
    <td>{{ item.quantity }}</td>
    <td>{{ FormatCurrency item.amount currency }}</td>
  </tr>
  {{ end }}
</table>
```

## 🎨 Supported HTML Tags

✅ **Supported:**
- Headings: `<h1>`, `<h2>`, `<h3>`, `<h4>`, `<h5>`, `<h6>`
- Paragraphs: `<p>`, `<div>`
- Tables: `<table>`, `<tr>`, `<td>`, `<th>`
- Formatting: `<strong>`, `<em>`, `<br>`

❌ **Not Supported:**
- CSS: flexbox, grid, animations
- Media: `<img>`, `<svg>`, `<canvas>`
- Forms: `<input>`, `<button>`, `<form>`
- Advanced: `<iframe>`, `<script>`, `<style>`

## 💡 Common Patterns

### Invoice Header
```html
<h1>INVOICE</h1>
<h3>Invoice #{{ invoiceNumber }}</h3>
<p>Date: {{ FormatDate date "MMMM dd, yyyy" }}</p>
```

### Customer Information Block
```html
<h3>Bill To:</h3>
<p>
  <strong>{{ customer.name }}</strong><br>
  {{ customer.address.street }}<br>
  {{ customer.address.city }}, {{ customer.address.postalCode }}<br>
  {{ customer.address.country }}<br>
  VAT: {{ customer.fiscalId }}
</p>
```

### Line Items Table
```html
<h3>Services & Charges</h3>
<table>
  <tr>
    <th width="60%">Description</th>
    <th width="20%">Quantity</th>
    <th width="20%">Amount</th>
  </tr>
  
  {{ for item in lineItems }}
  <tr>
    <td>{{ item.description }}</td>
    <td>{{ item.quantity }}</td>
    <td>{{ FormatCurrency item.amount currency }}</td>
  </tr>
  {{ end }}
  
  <tr>
    <td><strong>Subtotal</strong></td>
    <td></td>
    <td><strong>{{ FormatCurrency subtotal currency }}</strong></td>
  </tr>
</table>
```

### Tax Breakdown
```html
{{ for tax in taxes }}
<tr>
  <td>{{ tax.description }} ({{ FormatDecimal tax.rate 2 }}%)</td>
  <td></td>
  <td>{{ FormatCurrency tax.amount currency }}</td>
</tr>
{{ end }}
```

### Total Row
```html
<tr>
  <td><h3>TOTAL</h3></td>
  <td></td>
  <td><h3>{{ FormatCurrency total currency }}</h3></td>
</tr>
```

### Monthly Invoice Footer
```html
{{ if workedDays }}
<p>
  <strong>Period:</strong> {{ monthDescription }}<br>
  <strong>Days Worked:</strong> {{ workedDays }}<br>
  <strong>Daily Rate:</strong> {{ FormatCurrency monthlyRate currency }}
</p>
{{ end }}
```

## ⚠️ Common Mistakes

### ❌ Wrong: PascalCase
```html
{{ InvoiceNumber }}
{{ Customer.Name }}
```

### ✅ Correct: camelCase
```html
{{ invoiceNumber }}
{{ customer.name }}
```

### ❌ Wrong: Missing end tag
```html
{{ for item in lineItems }}
  <tr><td>{{ item.description }}</td></tr>
<!-- Missing {{ end }} -->
```

### ✅ Correct: Always close loops
```html
{{ for item in lineItems }}
  <tr><td>{{ item.description }}</td></tr>
{{ end }}
```

### ❌ Wrong: Incorrect function syntax
```html
{{ FormatCurrency(total, currency) }}
```

### ✅ Correct: Space-separated parameters
```html
{{ FormatCurrency total currency }}
```

## 🔍 Debugging Tips

1. **Use template validation** before saving
2. **Check placeholder spelling** (case-sensitive!)
3. **Verify all loops have {{ end }}**
4. **Test with sample template** first
5. **Check validation errors** for line numbers

## 📚 Additional Resources

- See `implementation-complete-summary.md` for full documentation
- Use "Load Sample" button in editor for working example
- Check placeholder panel in editor for all available variables

---

**Quick Start:** Load sample template → Modify → Validate → Save → Generate invoice
