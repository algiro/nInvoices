import { InvoiceStatus, InvoiceType } from '@/types'

type Tone = 'neutral' | 'info' | 'success' | 'warning' | 'danger'

/** 1234.5 EUR -> "€1,234.50" in the viewer's locale; falls back to "1234.50 EUR" for unknown codes. */
export function formatMoney(amount: number | null | undefined, currency: string | null | undefined, locale?: string): string {
  if (amount === null || amount === undefined || Number.isNaN(amount)) return '—'
  try {
    return new Intl.NumberFormat(locale, { style: 'currency', currency: currency || 'EUR' }).format(amount)
  } catch {
    return `${amount.toFixed(2)} ${currency ?? ''}`.trim()
  }
}

/** ISO date or date-time -> "24 Sep 2026" (viewer's locale). Plain dates are not shifted by time zone. */
export function formatDate(value: string | null | undefined, options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' }): string {
  if (!value) return '—'
  const date = /^\d{4}-\d{2}-\d{2}$/.test(value) ? new Date(`${value}T00:00:00`) : new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString(undefined, options)
}

/** Month number 1-12 and year -> "September 2026". */
export function formatPeriod(month: number | null | undefined, year: number | null | undefined): string {
  if (!month || !year) return '—'
  return new Date(year, month - 1, 1).toLocaleDateString(undefined, { month: 'long', year: 'numeric' })
}

// The API serialises enums as names ("Paid"); older payloads may carry the numeric value
function enumName<T extends Record<string, string | number>>(enumType: T, value: unknown): string {
  if (typeof value === 'number') return String(enumType[value as unknown as keyof T] ?? value)
  return String(value ?? '')
}

const statusMeta: Record<string, { label: string; tone: Tone }> = {
  Draft: { label: 'Draft', tone: 'neutral' },
  Finalized: { label: 'Finalized', tone: 'info' },
  Sent: { label: 'Sent', tone: 'warning' },
  Paid: { label: 'Paid', tone: 'success' },
  Cancelled: { label: 'Cancelled', tone: 'danger' }
}

export function invoiceStatus(status: InvoiceStatus | string | number): { label: string; tone: Tone; name: string } {
  const name = enumName(InvoiceStatus, status)
  return { name, ...(statusMeta[name] ?? { label: name, tone: 'neutral' }) }
}

const typeLabels: Record<string, string> = {
  Monthly: 'Monthly',
  OneTime: 'One-time',
  Quarterly: 'Quarterly',
  Annual: 'Annual'
}

export function invoiceTypeLabel(type: InvoiceType | string | number): string {
  const name = enumName(InvoiceType, type)
  return typeLabels[name] ?? name
}

/** "it-IT" -> "Italian (Italy)"; an empty locale reads "Not set". */
export function localeLabel(locale: string | null | undefined): string {
  if (!locale) return 'Not set'
  try {
    return new Intl.DisplayNames(undefined, { type: 'language' }).of(locale) ?? locale
  } catch {
    return locale
  }
}
