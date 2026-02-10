# nInvoices Template Placeholders Guide

## Overview

nInvoices uses **Scriban** template engine for generating invoices and monthly reports. Scriban is a fast, safe, and powerful templating language with a syntax similar to Liquid.

**Custom Delimiter:** Templates use `[[ ]]` instead of `{{ }}` to avoid conflicts with Vue.js on the frontend.

---

## Quick Start

### Basic Placeholder
```html
<p>Invoice Number: [[ invoiceNumber ]]</p>
<p>Customer: [[ customer.name ]]</p>
```

### With Formatting
```html
<p>Total: [[ FormatCurrency total currency ]]</p>
<p>Date: [[ FormatDate date "dd/MM/yyyy" ]]</p>
```

### Conditionals
```html
[[ if dueDate ]]
<p>Due Date: [[ FormatDate dueDate "dd/MM/yyyy" ]]</p>
[[ end ]]
```

### Loops
```html
[[ for item in lineItems ]]
<tr>
  <td>[[ item.description ]]</td>
  <td>[[ FormatCurrency item.amount currency ]]</td>
</tr>
[[ end ]]
```

---

## Data Models

### Invoice Template Model

| Property | Type | Description | Example |
|----------|------|-------------|---------|
| `invoiceNumber` | string | Invoice number | "INV-2024-001" |
| `invoiceType` | string | Type of invoice | "Standard", "MonthlyRate" |
| `date` | DateTime | Invoice date | |
| `dueDate` | DateTime? | Payment due date (nullable) | |
| `currency` | string | ISO currency code | "EUR", "USD", "GBP" |
| `workedDays` | int? | Number of days worked (nullable) | 20 |
| `monthNumber` | int? | Month number for monthly invoices | 1-12 |
| `monthDescription` | string? | Month name in locale | "January", "Gennaio" |
| `monthlyRate` | decimal? | Monthly rate amount | 3000.00 |
| `totalExpenses` | decimal? | Total expenses amount | 150.00 |
| `subtotal` | decimal | Subtotal before taxes | 3150.00 |
| `totalTax` | decimal | Total tax amount | 661.50 |
| `total` | decimal | Grand total | 3811.50 |

#### Nested Objects

**`customer`** (CustomerTemplateModel):
```
customer.name         - Customer name
customer.fiscalId     - Tax/VAT ID
customer.address      - Address object:
  - street            - Street address
  - city              - City name
  - postalCode        - ZIP/postal code
  - country           - Country name
```

**`lineItems`** (List&lt;LineItemTemplateModel&gt;):
```
lineItems             - Collection of invoice lines
  - description       - Line item description
  - quantity          - Quantity/hours/days
  - rate              - Unit price/rate
  - amount            - Line total (quantity × rate)
```

**`taxes`** (List&lt;TaxTemplateModel&gt;):
```
taxes                 - Collection of taxes
  - description       - Tax name ("VAT 21%", "Withholding -15%")
  - rate              - Tax rate percentage (can be negative)
  - amount            - Tax amount (can be negative for withholdings)
```

### Monthly Report Template Model

| Property | Type | Description | Example |
|----------|------|-------------|---------|
| `customerName` | string | Customer name | "Acme Corp" |
| `locale` | string | Locale for localization | "it-IT", "en-US" |
| `year` | int | Report year | 2024 |
| `monthNumber` | int | Month (1-12) | 3 |
| `dailyRate` | decimal? | Daily rate | 300.00 |
| `currency` | string? | ISO currency code | "EUR" |
| `totalAmount` | decimal? | Total calculated amount | 6300.00 |
| `workedDaysCount` | int | Count of worked days | 21 |
| `publicHolidayCount` | int | Count of public holidays | 2 |
| `unpaidLeaveCount` | int | Count of unpaid leave days | 1 |
| `totalDaysInMonth` | int | Days in month | 31 |

**`customer`** (CustomerTemplateModel) - Optional:
Same as invoice template

**`monthDays`** (List&lt;MonthDayTemplateModel&gt;):
```
monthDays              - Collection of all days in the month
  - dateValue          - Date object
  - dayNumber          - Day of month (1-31)
  - type               - "Worked", "PublicHoliday", "UnpaidLeave", or empty
  - isWeekend          - Boolean: true if Saturday/Sunday
  - isWorked           - Boolean: true if worked day
  - isPublicHoliday    - Boolean: true if holiday
  - isUnpaidLeave      - Boolean: true if leave
  - notes              - Optional notes/description
```

---

## Iteration Examples

### Basic Loop (Invoice Line Items)
```html
<table>
  <thead>
    <tr>
      <th>Description</th>
      <th>Qty</th>
      <th>Rate</th>
      <th>Amount</th>
    </tr>
  </thead>
  <tbody>
    [[ for item in lineItems ]]
    <tr>
      <td>[[ item.description ]]</td>
      <td>[[ item.quantity ]]</td>
      <td>[[ FormatCurrency item.rate currency ]]</td>
      <td>[[ FormatCurrency item.amount currency ]]</td>
    </tr>
    [[ end ]]
  </tbody>
</table>
```

### Loop with Index
```html
[[ for item in lineItems ]]
<tr>
  <td>[[ for.index + 1 ]]</td>  <!-- Row number starting at 1 -->
  <td>[[ item.description ]]</td>
</tr>
[[ end ]]
```

Available loop variables:
- `for.index` - Zero-based index (0, 1, 2...)
- `for.first` - true if first iteration
- `for.last` - true if last iteration
- `for.even` - true if even iteration
- `for.odd` - true if odd iteration

### Taxes Loop (Including Negative Values)
```html
<table>
  <tbody>
    [[ for tax in taxes ]]
    <tr>
      <td>[[ tax.description ]]</td>
      <td>[[ FormatDecimal tax.rate 2 ]]%</td>
      <td>[[ FormatCurrency tax.amount currency ]]</td>
    </tr>
    [[ end ]]
  </tbody>
</table>
```

### Monthly Report Calendar Loop
```html
<table>
  <thead>
    <tr>
      <th>Date</th>
      <th>Day</th>
      <th>Type</th>
      <th>Notes</th>
    </tr>
  </thead>
  <tbody>
    [[ for day in monthDays ]]
    <tr class="[[ if day.isWeekend ]]weekend[[ else if day.isWorked ]]worked[[ else if day.isPublicHoliday ]]holiday[[ end ]]">
      <td>[[ day.dateValue | date.to_string "%d/%m/%Y" ]]</td>
      <td>[[ LocalizeDayOfWeek day.dateValue locale ]]</td>
      <td>[[ day.type ]]</td>
      <td>[[ day.notes ]]</td>
    </tr>
    [[ end ]]
  </tbody>
</table>
```

---

## Conditional Logic

### Simple If
```html
[[ if dueDate ]]
<p>Payment due by: [[ FormatDate dueDate "dd/MM/yyyy" ]]</p>
[[ end ]]
```

### If-Else
```html
[[ if workedDays ]]
<p>Worked Days: [[ workedDays ]]</p>
[[ else ]]
<p>No worked days recorded</p>
[[ end ]]
```

### If-Else If-Else
```html
[[ if day.isWorked ]]
  <span class="badge-green">Worked</span>
[[ else if day.isPublicHoliday ]]
  <span class="badge-orange">Holiday</span>
[[ else if day.isUnpaidLeave ]]
  <span class="badge-blue">Leave</span>
[[ else ]]
  <span class="badge-gray">-</span>
[[ end ]]
```

### Comparison Operators
```html
[[ if total > 1000 ]]
  <p class="warning">High value invoice</p>
[[ end ]]

[[ if taxes.size == 0 ]]
  <p>No taxes applied</p>
[[ end ]]

[[ if invoiceType == "MonthlyRate" ]]
  <div class="monthly-section">...</div>
[[ end ]]
```

Operators: `==`, `!=`, `<`, `<=`, `>`, `>=`, `&&` (and), `||` (or), `!` (not)

---

## Custom Helper Functions

### FormatCurrency
Formats a number as currency with proper locale formatting.

**Syntax:** `FormatCurrency(amount, currency)`

```html
[[ FormatCurrency total "EUR" ]]      <!-- €1.234,56 -->
[[ FormatCurrency total "USD" ]]      <!-- $1,234.56 -->
[[ FormatCurrency total "GBP" ]]      <!-- £1,234.56 -->
```

### FormatDate
Formats a date with custom format string.

**Syntax:** `FormatDate(date, format)`

```html
[[ FormatDate date "dd/MM/yyyy" ]]           <!-- 23/01/2024 -->
[[ FormatDate date "yyyy-MM-dd" ]]           <!-- 2024-01-23 -->
[[ FormatDate date "MMMM dd, yyyy" ]]        <!-- January 23, 2024 -->
[[ FormatDate date "dd MMM yy" ]]            <!-- 23 Jan 24 -->
```

Format codes:
- `dd` - Day with leading zero (01-31)
- `MM` - Month with leading zero (01-12)
- `MMM` - Short month name (Jan, Feb)
- `MMMM` - Full month name (January, February)
- `yy` - Two-digit year (24)
- `yyyy` - Four-digit year (2024)

### FormatDecimal
Formats a decimal number with specified decimal places.

**Syntax:** `FormatDecimal(value, decimals)`

```html
[[ FormatDecimal tax.rate 2 ]]        <!-- 21.00 -->
[[ FormatDecimal tax.rate 1 ]]        <!-- 21.0 -->
[[ FormatDecimal item.quantity 0 ]]   <!-- 5 -->
```

### LocalizeMonth
Gets localized month name.

**Syntax:** `LocalizeMonth(monthNumber, locale, shortFormat?)`

```html
[[ LocalizeMonth 1 "en-US" ]]         <!-- January -->
[[ LocalizeMonth 1 "en-US" true ]]    <!-- Jan -->
[[ LocalizeMonth 3 "it-IT" ]]         <!-- Marzo -->
[[ LocalizeMonth 12 "es-ES" ]]        <!-- Diciembre -->
```

### LocalizeDayOfWeek
Gets localized day of week name.

**Syntax:** `LocalizeDayOfWeek(date, locale, shortFormat?)`

```html
[[ LocalizeDayOfWeek day.dateValue "en-US" ]]      <!-- Monday -->
[[ LocalizeDayOfWeek day.dateValue "en-US" true ]] <!-- Mon -->
[[ LocalizeDayOfWeek day.dateValue "it-IT" ]]      <!-- Lunedì -->
[[ LocalizeDayOfWeek day.dateValue "es-ES" ]]      <!-- Lunes -->
```

---

## Built-in Scriban Filters

Scriban provides many built-in filters accessed with the pipe `|` operator.

### String Filters
```html
[[ customer.name | upcase ]]                    <!-- UPPERCASE -->
[[ customer.name | downcase ]]                  <!-- lowercase -->
[[ customer.name | capitalize ]]                <!-- Capitalize First -->
[[ item.description | truncate 50 ]]            <!-- Truncate to 50 chars -->
[[ item.description | replace "old" "new" ]]    <!-- Replace text -->
```

### Date Filters
```html
[[ date | date.to_string "%d/%m/%Y" ]]          <!-- 23/01/2024 -->
[[ date | date.to_string "%A, %B %d, %Y" ]]     <!-- Monday, January 23, 2024 -->
[[ date | date.add_days 30 ]]                   <!-- Add 30 days -->
[[ date | date.year ]]                          <!-- 2024 -->
[[ date | date.month ]]                         <!-- 1 -->
```

Format codes for `date.to_string`:
- `%d` - Day (01-31)
- `%m` - Month (01-12)
- `%Y` - Year (2024)
- `%y` - Short year (24)
- `%B` - Full month (January)
- `%b` - Short month (Jan)
- `%A` - Full weekday (Monday)
- `%a` - Short weekday (Mon)

### Number Filters
```html
[[ total | math.round 2 ]]                      <!-- Round to 2 decimals -->
[[ total | math.floor ]]                        <!-- Round down -->
[[ total | math.ceil ]]                         <!-- Round up -->
[[ value | math.abs ]]                          <!-- Absolute value -->
```

### Collection Filters
```html
[[ lineItems | size ]]                          <!-- Number of items -->
[[ lineItems | first ]]                         <!-- First item -->
[[ lineItems | last ]]                          <!-- Last item -->
[[ taxes | sort "rate" ]]                       <!-- Sort by property -->
```

### Default Values
```html
[[ notes | default "No notes" ]]                <!-- Show default if empty/null -->
[[ customer.address.street | default "N/A" ]]
```

---

## Complete Examples

### Professional Invoice Template
```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Invoice [[ invoiceNumber ]]</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; }
        .header { display: flex; justify-content: space-between; margin-bottom: 40px; }
        .invoice-title { font-size: 32px; color: #2563eb; }
        table { width: 100%; border-collapse: collapse; margin: 20px 0; }
        th { background: #2563eb; color: white; padding: 10px; text-align: left; }
        td { padding: 10px; border-bottom: 1px solid #e5e7eb; }
        .totals { text-align: right; }
        .grand-total { font-size: 20px; font-weight: bold; color: #2563eb; }
    </style>
</head>
<body>
    <!-- Header -->
    <div class="header">
        <div>
            <h1 class="invoice-title">INVOICE</h1>
            <p><strong>[[ invoiceNumber ]]</strong></p>
            <p>Date: [[ FormatDate date "dd/MM/yyyy" ]]</p>
            [[ if dueDate ]]
            <p>Due: [[ FormatDate dueDate "dd/MM/yyyy" ]]</p>
            [[ end ]]
        </div>
        <div style="text-align: right;">
            <h2>[[ customer.name ]]</h2>
            <p>[[ customer.fiscalId ]]</p>
            <p>[[ customer.address.street ]]</p>
            <p>[[ customer.address.postalCode ]] [[ customer.address.city ]]</p>
            <p>[[ customer.address.country ]]</p>
        </div>
    </div>

    <!-- Line Items -->
    <table>
        <thead>
            <tr>
                <th style="width: 50%;">Description</th>
                <th style="width: 15%; text-align: center;">Quantity</th>
                <th style="width: 17.5%; text-align: right;">Rate</th>
                <th style="width: 17.5%; text-align: right;">Amount</th>
            </tr>
        </thead>
        <tbody>
            [[ for item in lineItems ]]
            <tr>
                <td>[[ item.description ]]</td>
                <td style="text-align: center;">[[ item.quantity ]]</td>
                <td style="text-align: right;">[[ FormatCurrency item.rate currency ]]</td>
                <td style="text-align: right;">[[ FormatCurrency item.amount currency ]]</td>
            </tr>
            [[ end ]]
        </tbody>
    </table>

    <!-- Totals -->
    <div class="totals">
        <p><strong>Subtotal:</strong> [[ FormatCurrency subtotal currency ]]</p>
        
        [[ if taxes.size > 0 ]]
        [[ for tax in taxes ]]
        <p>[[ tax.description ]]: [[ FormatCurrency tax.amount currency ]]</p>
        [[ end ]]
        [[ end ]]
        
        <p class="grand-total">
            <strong>Total:</strong> [[ FormatCurrency total currency ]]
        </p>
    </div>
</body>
</html>
```

### Monthly Report with Calendar
```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Monthly Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        h1 { color: #1e40af; border-bottom: 2px solid #1e40af; padding-bottom: 10px; }
        .summary { background: #f3f4f6; padding: 15px; border-radius: 5px; margin: 20px 0; }
        table { width: 100%; border-collapse: collapse; margin-top: 20px; }
        th { background: #1e40af; color: white; padding: 10px; text-align: left; }
        td { padding: 8px; border: 1px solid #e5e7eb; }
        .worked { background: #ccffcc; }
        .public-holiday { background: #fff3e0; }
        .unpaid-leave { background: #99ccff; }
        .weekend { background: #f9fafb; color: #9ca3af; }
    </style>
</head>
<body>
    <h1>Monthly Report - [[ LocalizeMonth monthNumber locale ]] [[ year ]]</h1>
    
    <div class="summary">
        <p><strong>Customer:</strong> [[ customerName ]]</p>
        <p><strong>Worked Days:</strong> [[ workedDaysCount ]]</p>
        <p><strong>Public Holidays:</strong> [[ publicHolidayCount ]]</p>
        <p><strong>Unpaid Leave:</strong> [[ unpaidLeaveCount ]]</p>
        [[ if dailyRate && currency ]]
        <p><strong>Daily Rate:</strong> [[ FormatCurrency dailyRate currency ]]</p>
        <p><strong>Total Amount:</strong> [[ FormatCurrency totalAmount currency ]]</p>
        [[ end ]]
    </div>

    <table>
        <thead>
            <tr>
                <th>Date</th>
                <th>Day</th>
                <th>Type</th>
                <th>Notes</th>
            </tr>
        </thead>
        <tbody>
            [[ for day in monthDays ]]
            <tr class="[[ if day.isWeekend ]]weekend[[ else if day.isWorked ]]worked[[ else if day.isPublicHoliday ]]public-holiday[[ else if day.isUnpaidLeave ]]unpaid-leave[[ end ]]">
                <td>[[ day.dateValue | date.to_string "%d/%m/%Y" ]]</td>
                <td>[[ LocalizeDayOfWeek day.dateValue locale ]]</td>
                <td>
                    [[ if day.isWorked ]]✓ Worked
                    [[ else if day.isPublicHoliday ]]🎉 Holiday
                    [[ else if day.isUnpaidLeave ]]📅 Leave
                    [[ else if day.isWeekend ]]⊗ Weekend
                    [[ else ]]-
                    [[ end ]]
                </td>
                <td>[[ day.notes | default "" ]]</td>
            </tr>
            [[ end ]]
        </tbody>
    </table>
</body>
</html>
```

---

## Template Validation

Before saving a template, it's automatically validated by the system:

### Validation Checks
1. **Empty template** - Template must not be empty
2. **Syntax errors** - All `[[ ]]` tags must be properly closed
3. **Invalid Scriban syntax** - Filter and function calls must be valid

### Error Messages
Validation errors include line and column numbers:
```
Syntax error at line 15, column 8: Unexpected end of template
Missing closing tag for 'for' statement at line 23
Unknown function 'InvalidFunction' at line 42
```

### Testing Templates
1. Use the template editor's "Validate" button
2. Check for placeholder extraction in validation results
3. Preview with sample data before finalizing
4. Test with actual invoice/report generation

---

## Tips and Best Practices

### 1. Always Handle Null Values
```html
<!-- Good: Safe navigation -->
[[ if customer.address ]]
<p>[[ customer.address.street ]]</p>
[[ end ]]

<!-- Better: With default -->
<p>[[ customer.address.street | default "Address not provided" ]]</p>
```

### 2. Use Proper Number Formatting
```html
<!-- Good: Use FormatCurrency for money -->
[[ FormatCurrency total currency ]]

<!-- Good: Use FormatDecimal for percentages/rates -->
[[ FormatDecimal tax.rate 2 ]]%
```

### 3. Consistent Date Formatting
```html
<!-- Choose one format and stick to it -->
[[ FormatDate date "dd/MM/yyyy" ]]    <!-- European -->
[[ FormatDate date "MM/dd/yyyy" ]]    <!-- US -->
[[ FormatDate date "yyyy-MM-dd" ]]    <!-- ISO -->
```

### 4. Use CSS Classes for Styling
```html
<!-- Good: Conditional classes -->
<tr class="[[ if day.isWeekend ]]weekend[[ end ]]">

<!-- Better: Multiple conditions -->
<tr class="[[ if day.isWeekend ]]weekend[[ else if day.isWorked ]]worked[[ end ]]">
```

### 5. Check Collection Sizes
```html
[[ if lineItems.size > 0 ]]
<table>
  [[ for item in lineItems ]]
  ...
  [[ end ]]
</table>
[[ else ]]
<p>No items</p>
[[ end ]]
```

### 6. Localization
```html
<!-- Use locale-aware formatting -->
[[ LocalizeMonth monthNumber locale ]]
[[ LocalizeDayOfWeek date locale ]]

<!-- Currency will format based on locale -->
[[ FormatCurrency amount "EUR" ]]  <!-- €1.234,56 in EU, €1,234.56 in US -->
```

---

## Common Patterns

### Invoice Footer
```html
<div class="footer">
    <p>Payment due within 30 days</p>
    [[ if dueDate ]]
    <p><strong>Due Date:</strong> [[ FormatDate dueDate "MMMM dd, yyyy" ]]</p>
    [[ end ]]
    <p>Thank you for your business!</p>
</div>
```

### Subtotal Breakdown
```html
<div class="totals">
    <div class="total-row">
        <span>Subtotal:</span>
        <span>[[ FormatCurrency subtotal currency ]]</span>
    </div>
    
    [[ for tax in taxes ]]
    <div class="total-row">
        <span>[[ tax.description ]] ([[ FormatDecimal tax.rate 2 ]]%):</span>
        <span>[[ FormatCurrency tax.amount currency ]]</span>
    </div>
    [[ end ]]
    
    <div class="total-row grand-total">
        <span><strong>Total:</strong></span>
        <span><strong>[[ FormatCurrency total currency ]]</strong></span>
    </div>
</div>
```

### Conditional Sections
```html
[[ if invoiceType == "MonthlyRate" ]]
<div class="monthly-details">
    <h3>Monthly Rate Invoice</h3>
    <p>Month: [[ monthDescription ]]</p>
    <p>Days Worked: [[ workedDays ]] days</p>
    <p>Monthly Rate: [[ FormatCurrency monthlyRate currency ]]</p>
</div>
[[ end ]]

[[ if totalExpenses > 0 ]]
<div class="expenses">
    <h3>Expenses</h3>
    <p>Total Expenses: [[ FormatCurrency totalExpenses currency ]]</p>
</div>
[[ end ]]
```

---

## Troubleshooting

### Issue: Placeholder not rendering
**Check:**
- Spelling matches the model property exactly
- Property exists in the model (case-sensitive after conversion to camelCase)
- Use `[[ ]]` delimiters, not `{{ }}`

### Issue: Date showing weird format
**Solution:** Use `FormatDate` helper or Scriban's date filter
```html
<!-- Wrong: Raw date object -->
[[ date ]]

<!-- Right: Formatted -->
[[ FormatDate date "dd/MM/yyyy" ]]
[[ date | date.to_string "%d/%m/%Y" ]]
```

### Issue: Currency showing raw number
**Solution:** Use `FormatCurrency` helper
```html
<!-- Wrong: -->
[[ total ]]

<!-- Right: -->
[[ FormatCurrency total currency ]]
```

### Issue: Loop not working
**Check:**
- Collection name is correct
- `[[ end ]]` tag is present
- No typos in loop variable

### Issue: Conditional always executing
**Check:**
- Comparison operators: `==` not `=`
- Boolean properties: `[[ if isActive ]]` not `[[ if isActive == true ]]`
- Null checks: Use `[[ if property ]]` to check existence

---

## Reference Links

- [Scriban Language Documentation](https://github.com/scriban/scriban/blob/master/doc/language.md)
- [Scriban Built-in Functions](https://github.com/scriban/scriban/blob/master/doc/builtins.md)
- [Date Format Codes](https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings)

---

## Getting Help

If you encounter issues with templates:
1. Use the template validation feature to catch syntax errors
2. Check this guide for available placeholders and functions
3. Review the default templates in `Docs/` folder
4. Test with small sections first before building complex templates
5. Remember: Property names are automatically converted to camelCase in templates
