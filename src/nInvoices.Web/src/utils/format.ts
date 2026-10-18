import { ExpenseCategory, InvoiceStatus, InvoiceType } from '@/types'
import { currentLanguage, t, type MessageKey } from '@/i18n'

type Tone = 'neutral' | 'info' | 'success' | 'warning' | 'danger'

// Dates, numbers and money read the way the app's language writes them (the language the user chose in
// Settings): they follow it when it changes.

/** 1234.5 EUR -> "€1,234.50" in the app's language; falls back to "1234.50 EUR" for unknown codes. */
export function formatMoney(amount: number | null | undefined, currency: string | null | undefined, locale?: string): string {
  if (amount === null || amount === undefined || Number.isNaN(amount)) return '—'
  try {
    return new Intl.NumberFormat(locale ?? currentLanguage(), { style: 'currency', currency: currency || 'EUR' }).format(amount)
  } catch {
    return `${amount.toFixed(2)} ${currency ?? ''}`.trim()
  }
}

/** ISO date or date-time -> "24 Sep 2026" (app's language). Plain dates are not shifted by time zone. */
export function formatDate(value: string | null | undefined, options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' }): string {
  if (!value) return '—'
  const date = /^\d{4}-\d{2}-\d{2}$/.test(value) ? new Date(`${value}T00:00:00`) : new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString(currentLanguage(), options)
}

/** Month number 1-12 and year -> "September 2026". */
export function formatPeriod(month: number | null | undefined, year: number | null | undefined): string {
  if (!month || !year) return '—'
  return new Date(year, month - 1, 1).toLocaleDateString(currentLanguage(), { month: 'long', year: 'numeric' })
}

const statusMeta: Record<string, { label: MessageKey; tone: Tone }> = {
  Draft: { label: 'invoiceStatus.Draft', tone: 'neutral' },
  Finalized: { label: 'invoiceStatus.Finalized', tone: 'info' },
  Sent: { label: 'invoiceStatus.Sent', tone: 'warning' },
  Paid: { label: 'invoiceStatus.Paid', tone: 'success' },
  Cancelled: { label: 'invoiceStatus.Cancelled', tone: 'danger' }
}

export function invoiceStatus(status: InvoiceStatus | string): { label: string; tone: Tone; name: string } {
  const name = String(status ?? '')
  const meta = statusMeta[name]
  return meta ? { name, label: t(meta.label), tone: meta.tone } : { name, label: name, tone: 'neutral' }
}

const typeLabels: Record<string, MessageKey> = {
  Monthly: 'invoiceType.Monthly',
  OneTime: 'invoiceType.OneTime',
  Quarterly: 'invoiceType.Quarterly',
  Annual: 'invoiceType.Annual'
}

export function invoiceTypeLabel(type: InvoiceType | string): string {
  const name = String(type ?? '')
  const key = typeLabels[name]
  return key ? t(key) : name
}

/** An expense category's name in the app's language. */
export function expenseCategoryLabel(category: ExpenseCategory | string): string {
  const name = String(category ?? '')
  return name in ExpenseCategory ? t(`expenseCategory.${name as ExpenseCategory}`) : name
}

/** "it-IT" -> "Italian (Italy)" in the app's language; an empty locale reads "Not set". */
export function localeLabel(locale: string | null | undefined): string {
  if (!locale) return t('common.notSet')
  try {
    return new Intl.DisplayNames(currentLanguage(), { type: 'language' }).of(locale) ?? locale
  } catch {
    return locale
  }
}
