/**
 * What a template author can use, per template kind. Mirrors the backend models
 * (InvoiceTemplateModel, MonthlyReportTemplateModel, EmailTemplateModel) and the functions registered in
 * ScribanTemplateRenderer; property names are camelCase as the renderer exposes them.
 */

import { t } from '@/i18n'

// Titles and descriptions below are message keys (templateVars.*); variablesFor translates them.

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
  title: 'templateVars.groups.customer',
  items: [
    v('customer.name', 'templateVars.items.customerName'),
    v('customer.fiscalId', 'templateVars.items.customerFiscalId'),
    v('customer.address.street', 'templateVars.items.customerAddressStreet'),
    v('customer.address.postalCode', 'templateVars.items.customerAddressPostalCode'),
    v('customer.address.city', 'templateVars.items.customerAddressCity'),
    v('customer.address.country', 'templateVars.items.customerAddressCountry')
  ]
}

const functionsGroup: VariableGroup = {
  title: 'templateVars.groups.functions',
  items: [
    { insert: '[[ FormatCurrency $0 currency ]]', label: 'FormatCurrency amount currency locale?', description: 'templateVars.items.formatCurrencyAmountCurrencyLocale', completion: 'FormatCurrency' },
    { insert: '[[ FormatDecimal $0 2 ]]', label: 'FormatDecimal value decimals locale?', description: 'templateVars.items.formatDecimalValueDecimalsLocale', completion: 'FormatDecimal' },
    { insert: '[[ FormatDate $0 "dd/MM/yyyy" ]]', label: 'FormatDate date "format" locale?', description: 'templateVars.items.formatDateDateFormatLocale', completion: 'FormatDate' },
    { insert: '[[ Image "$0" ]]', label: 'Image "alias" width height', description: 'templateVars.items.imageAliasWidthHeight', completion: 'Image' }
  ]
}

const projectSummaryItem: TemplateVariable = {
  insert: '[[ for p in projectSummary ]]\n  [[ p.name ]]: [[ p.workedDays ]] days, [[ p.totalHours ]]h, [[ p.amount ]]\n[[ end ]]',
  label: 'for p in projectSummary',
  description: 'templateVars.items.forPInProjectSummary'
}

export const invoiceVariables: VariableGroup[] = [
  {
    title: 'templateVars.groups.invoice',
    items: [
      v('invoiceNumber', 'templateVars.items.invoiceNumber'),
      v('invoiceType', 'templateVars.items.invoiceType'),
      v('date', 'templateVars.items.date'),
      v('dueDate', 'templateVars.items.dueDate'),
      v('currency', 'templateVars.items.currency'),
      v('locale', 'templateVars.items.locale'),
      v('subtotal', 'templateVars.items.subtotal'),
      v('totalTax', 'templateVars.items.totalTax'),
      v('total', 'templateVars.items.total'),
      v('totalExpenses', 'templateVars.items.totalExpenses')
    ]
  },
  customerGroup,
  {
    title: 'templateVars.groups.monthlyPeriod',
    items: [
      v('monthDescription', 'templateVars.items.monthDescription'),
      v('monthNumber', 'templateVars.items.monthNumber'),
      v('workedDays', 'templateVars.items.workedDays'),
      v('monthlyRate', 'templateVars.items.monthlyRate')
    ]
  },
  {
    title: 'templateVars.groups.loopsConditions',
    items: [
      {
        insert: '[[ for item in lineItems ]]\n<tr>\n  <td>[[ item.description ]]</td>\n  <td>[[ item.quantity ]]</td>\n  <td>[[ FormatCurrency item.amount currency ]]</td>\n</tr>\n[[ end ]]',
        label: 'for item in lineItems',
        description: 'templateVars.items.forItemInLineItems'
      },
      {
        insert: '[[ for tax in taxes ]]\n<tr>\n  <td>[[ tax.description ]] ([[ FormatDecimal tax.rate 2 ]]%)</td>\n  <td>[[ FormatCurrency tax.amount currency ]]</td>\n</tr>\n[[ end ]]',
        label: 'for tax in taxes',
        description: 'templateVars.items.forTaxInTaxes'
      },
      {
        insert: '[[ for day in workedDayItems ]]\n  [[ day.date ]] [[ day.hours ]]h\n[[ end ]]',
        label: 'for day in workedDayItems',
        description: 'templateVars.items.forDayInWorkedDayItems'
      },
      projectSummaryItem,
      { insert: '[[ if workedDays ]]\n  $0\n[[ end ]]', label: 'if workedDays … end', description: 'templateVars.items.ifWorkedDaysEnd' }
    ]
  },
  functionsGroup
]

export const monthlyReportVariables: VariableGroup[] = [
  {
    title: 'templateVars.groups.report',
    items: [
      v('year', 'templateVars.items.year'),
      v('monthNumber', 'templateVars.items.monthNumber'),
      { insert: '[[ LocalizeMonth monthNumber locale ]]', label: 'LocalizeMonth monthNumber locale', description: 'templateVars.items.localizeMonthMonthNumberLocale', completion: 'LocalizeMonth' },
      v('locale', 'templateVars.items.locale2'),
      v('customerName', 'templateVars.items.customerName'),
      v('workedDaysCount', 'templateVars.items.workedDaysCount'),
      v('publicHolidayCount', 'templateVars.items.publicHolidayCount'),
      v('unpaidLeaveCount', 'templateVars.items.unpaidLeaveCount'),
      v('totalDaysInMonth', 'templateVars.items.totalDaysInMonth'),
      v('dailyRate', 'templateVars.items.dailyRate'),
      v('currency', 'templateVars.items.currency2'),
      v('totalAmount', 'templateVars.items.totalAmount')
    ]
  },
  customerGroup,
  {
    title: 'templateVars.groups.days',
    items: [
      {
        insert: '[[ for day in monthDays ]]\n<tr [[ if day.isWeekend ]]class="weekend"[[ end ]]>\n  <td>[[ FormatDate day.dateValue "dd/MM/yyyy" ]]</td>\n  <td>[[ LocalizeDayOfWeek day.dateValue locale ]]</td>\n  <td>[[ if day.isWorked ]][[ day.hours ]][[ end ]]</td>\n  <td>[[ day.notes ]]</td>\n</tr>\n[[ end ]]',
        label: 'for day in monthDays',
        description: 'templateVars.items.forDayInMonthDays'
      },
      v('day.dateValue', 'templateVars.items.dayDateValue'),
      v('day.dayNumber', 'templateVars.items.dayDayNumber'),
      v('day.type', 'templateVars.items.dayType'),
      v('day.isWorked', 'templateVars.items.dayIsWorked'),
      v('day.isWeekend', 'templateVars.items.dayIsWeekend'),
      v('day.isPublicHoliday', 'templateVars.items.dayIsPublicHoliday'),
      v('day.isUnpaidLeave', 'templateVars.items.dayIsUnpaidLeave'),
      v('day.hours', 'templateVars.items.dayHours'),
      v('day.notes', 'templateVars.items.dayNotes'),
      { insert: '[[ for p in day.projects ]][[ p.name ]] [[ p.hours ]]h [[ end ]]', label: 'for p in day.projects', description: 'templateVars.items.forPInDayProjects' },
      { insert: '[[ LocalizeDayOfWeek day.dateValue locale ]]', label: 'LocalizeDayOfWeek date locale', description: 'templateVars.items.localizeDayOfWeekDateLocale', completion: 'LocalizeDayOfWeek' }
    ]
  },
  {
    title: 'templateVars.groups.projects',
    items: [projectSummaryItem]
  },
  functionsGroup
]

export const emailVariables: VariableGroup[] = [
  {
    title: 'templateVars.groups.invoice',
    items: [
      v('invoiceNumber', 'templateVars.items.invoiceNumber'),
      v('invoiceType', 'templateVars.items.invoiceType'),
      v('date', 'templateVars.items.date'),
      v('dueDate', 'templateVars.items.dueDate'),
      v('currency', 'templateVars.items.currency'),
      v('subtotal', 'templateVars.items.subtotal'),
      v('totalTax', 'templateVars.items.totalTax'),
      v('totalExpenses', 'templateVars.items.totalExpenses'),
      v('total', 'templateVars.items.total')
    ]
  },
  {
    title: 'templateVars.groups.monthlyPeriod',
    items: [
      v('monthDescription', 'templateVars.items.monthDescription2'),
      v('monthNumber', 'templateVars.items.monthNumber'),
      v('year', 'templateVars.items.year'),
      v('workedDays', 'templateVars.items.workedDays'),
      { insert: '[[ if monthDescription ]]\n  $0\n[[ end ]]', label: 'if monthDescription … end', description: 'templateVars.items.ifMonthDescriptionEnd' }
    ]
  },
  {
    title: 'templateVars.groups.customerSender',
    items: [
      v('customer.name', 'templateVars.items.customerName'),
      v('customer.email', 'templateVars.items.customerEmail'),
      v('customer.fiscalId', 'templateVars.items.customerFiscalId'),
      v('customer.address.city', 'templateVars.items.customerAddressCity'),
      v('locale', 'templateVars.items.locale2'),
      v('senderEmail', 'templateVars.items.senderEmail')
    ]
  },
  functionsGroup
]

/** The groups of a kind of template, with their titles and descriptions in the app's language. */
export function variablesFor(kind: TemplateKind): VariableGroup[] {
  const groups = kind === 'invoice' ? invoiceVariables : kind === 'email' ? emailVariables : monthlyReportVariables
  return groups.map(group => ({
    title: t(group.title),
    items: group.items.map(item => ({ ...item, description: t(item.description) }))
  }))
}
