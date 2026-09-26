/**
 * What a template author can use, per template kind. Mirrors the backend models
 * (InvoiceTemplateModel, MonthlyReportTemplateModel, EmailTemplateModel) and the functions registered in
 * ScribanTemplateRenderer; property names are camelCase as the renderer exposes them.
 */

export type TemplateKind = 'invoice' | 'monthly-report' | 'email'

export interface TemplateVariable {
  /** Text inserted into the template. `$0` marks where the cursor goes afterwards. */
  insert: string
  /** Short label shown in the palette and in autocomplete. */
  label: string
  description: string
  /** Name offered by autocomplete inside [[ ]]; omit for snippets that aren't a single name. */
  completion?: string
}

export interface VariableGroup {
  title: string
  items: TemplateVariable[]
}

const v = (path: string, description: string): TemplateVariable => ({
  insert: `[[ ${path} ]]`,
  label: path,
  description,
  completion: path
})

const customerGroup: VariableGroup = {
  title: 'Customer',
  items: [
    v('customer.name', 'Customer name'),
    v('customer.fiscalId', 'VAT number / fiscal ID'),
    v('customer.address.street', 'Street and house number'),
    v('customer.address.postalCode', 'Postal code'),
    v('customer.address.city', 'City'),
    v('customer.address.country', 'Country')
  ]
}

const functionsGroup: VariableGroup = {
  title: 'Functions',
  items: [
    { insert: '[[ FormatCurrency $0 currency ]]', label: 'FormatCurrency amount currency locale?', description: 'Money as 1.234,56 EUR; add a locale, e.g. "en-US", for 1,234.56 EUR', completion: 'FormatCurrency' },
    { insert: '[[ FormatDecimal $0 2 ]]', label: 'FormatDecimal value decimals locale?', description: 'Number rounded to N decimals; the locale sets the decimal separator', completion: 'FormatDecimal' },
    { insert: '[[ FormatDate $0 "dd/MM/yyyy" ]]', label: 'FormatDate date "format" locale?', description: '.NET date format, e.g. "dd/MM/yyyy"; the locale sets month and day names', completion: 'FormatDate' },
    { insert: '[[ Image "$0" ]]', label: 'Image "alias" width height', description: 'Image uploaded in Settings › Images (width/height optional)', completion: 'Image' }
  ]
}

const projectSummaryItem: TemplateVariable = {
  insert: '[[ for p in projectSummary ]]\n  [[ p.name ]]: [[ p.workedDays ]] days, [[ p.totalHours ]]h, [[ p.amount ]]\n[[ end ]]',
  label: 'for p in projectSummary',
  description: 'Totals per project for the period'
}

export const invoiceVariables: VariableGroup[] = [
  {
    title: 'Invoice',
    items: [
      v('invoiceNumber', 'Invoice number, e.g. 26-09-004'),
      v('invoiceType', 'Monthly, OneTime, …'),
      v('date', 'Issue date (use FormatDate)'),
      v('dueDate', 'Due date, may be empty'),
      v('currency', 'Currency code, e.g. EUR'),
      v('locale', 'Customer locale, e.g. it-IT (pass it to FormatDate, FormatCurrency…)'),
      v('subtotal', 'Total before taxes'),
      v('totalTax', 'Sum of all taxes'),
      v('total', 'Amount due'),
      v('totalExpenses', 'Sum of expenses')
    ]
  },
  customerGroup,
  {
    title: 'Monthly period',
    items: [
      v('monthDescription', 'Period, e.g. “September 2026”'),
      v('monthNumber', 'Month number 1–12'),
      v('workedDays', 'Number of worked days'),
      v('monthlyRate', 'Daily or monthly rate')
    ]
  },
  {
    title: 'Loops & conditions',
    items: [
      {
        insert: '[[ for item in lineItems ]]\n<tr>\n  <td>[[ item.description ]]</td>\n  <td>[[ item.quantity ]]</td>\n  <td>[[ FormatCurrency item.amount currency ]]</td>\n</tr>\n[[ end ]]',
        label: 'for item in lineItems',
        description: 'One row per line item: description, quantity, rate, amount'
      },
      {
        insert: '[[ for tax in taxes ]]\n<tr>\n  <td>[[ tax.description ]] ([[ FormatDecimal tax.rate 2 ]]%)</td>\n  <td>[[ FormatCurrency tax.amount currency ]]</td>\n</tr>\n[[ end ]]',
        label: 'for tax in taxes',
        description: 'One row per tax: description, rate, amount'
      },
      {
        insert: '[[ for day in workedDayItems ]]\n  [[ day.date ]] [[ day.hours ]]h\n[[ end ]]',
        label: 'for day in workedDayItems',
        description: 'Each worked day with its hours'
      },
      projectSummaryItem,
      { insert: '[[ if workedDays ]]\n  $0\n[[ end ]]', label: 'if workedDays … end', description: 'Only for monthly invoices' }
    ]
  },
  functionsGroup
]

export const monthlyReportVariables: VariableGroup[] = [
  {
    title: 'Report',
    items: [
      v('year', 'Year, e.g. 2026'),
      v('monthNumber', 'Month number 1–12'),
      { insert: '[[ LocalizeMonth monthNumber locale ]]', label: 'LocalizeMonth monthNumber locale', description: 'Month name in the customer’s language', completion: 'LocalizeMonth' },
      v('locale', 'Customer locale, e.g. it-IT'),
      v('customerName', 'Customer name'),
      v('workedDaysCount', 'Number of worked days'),
      v('publicHolidayCount', 'Number of public holidays'),
      v('unpaidLeaveCount', 'Number of unpaid leave days'),
      v('totalDaysInMonth', 'Days in the month'),
      v('dailyRate', 'Daily rate'),
      v('currency', 'Currency code'),
      v('totalAmount', 'Amount for the month')
    ]
  },
  customerGroup,
  {
    title: 'Days',
    items: [
      {
        insert: '[[ for day in monthDays ]]\n<tr [[ if day.isWeekend ]]class="weekend"[[ end ]]>\n  <td>[[ FormatDate day.dateValue "dd/MM/yyyy" ]]</td>\n  <td>[[ LocalizeDayOfWeek day.dateValue locale ]]</td>\n  <td>[[ if day.isWorked ]][[ day.hours ]][[ end ]]</td>\n  <td>[[ day.notes ]]</td>\n</tr>\n[[ end ]]',
        label: 'for day in monthDays',
        description: 'One row per calendar day'
      },
      v('day.dateValue', 'The date (use FormatDate / LocalizeDayOfWeek)'),
      v('day.dayNumber', 'Day of month 1–31'),
      v('day.type', 'Worked, PublicHoliday, UnpaidLeave or empty'),
      v('day.isWorked', 'true on worked days'),
      v('day.isWeekend', 'true on Saturday and Sunday'),
      v('day.isPublicHoliday', 'true on public holidays'),
      v('day.isUnpaidLeave', 'true on unpaid leave days'),
      v('day.hours', 'Hours worked (8 = full day)'),
      v('day.notes', 'The day’s note'),
      { insert: '[[ for p in day.projects ]][[ p.name ]] [[ p.hours ]]h [[ end ]]', label: 'for p in day.projects', description: 'Projects worked on that day' },
      { insert: '[[ LocalizeDayOfWeek day.dateValue locale ]]', label: 'LocalizeDayOfWeek date locale', description: 'Weekday name in the customer’s language', completion: 'LocalizeDayOfWeek' }
    ]
  },
  {
    title: 'Projects',
    items: [projectSummaryItem]
  },
  functionsGroup
]

export const emailVariables: VariableGroup[] = [
  {
    title: 'Invoice',
    items: [
      v('invoiceNumber', 'Invoice number, e.g. 26-09-004'),
      v('invoiceType', 'Monthly, OneTime, …'),
      v('date', 'Issue date (use FormatDate)'),
      v('dueDate', 'Due date, may be empty'),
      v('currency', 'Currency code, e.g. EUR'),
      v('subtotal', 'Total before taxes'),
      v('totalTax', 'Sum of all taxes'),
      v('totalExpenses', 'Sum of expenses'),
      v('total', 'Amount due')
    ]
  },
  {
    title: 'Monthly period',
    items: [
      v('monthDescription', 'Month name in the customer’s language'),
      v('monthNumber', 'Month number 1–12'),
      v('year', 'Year, e.g. 2026'),
      v('workedDays', 'Number of worked days'),
      { insert: '[[ if monthDescription ]]\n  $0\n[[ end ]]', label: 'if monthDescription … end', description: 'Only for monthly invoices' }
    ]
  },
  {
    title: 'Customer & sender',
    items: [
      v('customer.name', 'Customer name'),
      v('customer.email', 'Customer email address'),
      v('customer.fiscalId', 'VAT number / fiscal ID'),
      v('customer.address.city', 'City'),
      v('locale', 'Customer locale, e.g. it-IT'),
      v('senderEmail', 'Your connected Gmail address')
    ]
  },
  functionsGroup
]

export function variablesFor(kind: TemplateKind): VariableGroup[] {
  return kind === 'invoice' ? invoiceVariables : kind === 'email' ? emailVariables : monthlyReportVariables
}
