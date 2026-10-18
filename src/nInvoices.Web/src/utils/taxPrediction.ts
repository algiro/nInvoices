import type { TaxFormDto, TaxPeriodDto, TaxPredictionDto, TaxProfileDto, TaxRegimeDto } from '@/types'

type Tone = 'neutral' | 'info' | 'success' | 'warning' | 'danger'

export interface FormStatus {
  label: string
  tone: Tone
}

/**
 * Where a period's form stands on `today` (ISO date): paid when a payment is recorded; still open
 * while the period runs (the estimate keeps changing); then nothing to pay if it comes to zero, or due
 * until the deadline and overdue after it.
 */
export function formStatus(form: TaxFormDto, period: TaxPeriodDto, today: string): FormStatus {
  if (form.payment) return { label: 'Paid', tone: 'success' }
  if (today <= period.end) return { label: 'Period in progress', tone: 'neutral' }
  if (form.estimated <= 0) return { label: 'Nothing to pay', tone: 'neutral' }
  if (today <= form.deadline) return { label: 'Due', tone: 'warning' }
  return { label: 'Overdue', tone: 'danger' }
}

/** What the year comes to: the estimates of every form, and what was recorded as paid. */
export function yearTotals(prediction: TaxPredictionDto): { estimated: number; paid: number } {
  const forms = prediction.periods.flatMap(p => p.forms)
  return {
    estimated: round2(forms.reduce((sum, f) => sum + f.estimated, 0)),
    paid: round2(forms.reduce((sum, f) => sum + (f.payment?.amount ?? 0), 0))
  }
}

/** The next form to pay: the first unpaid one with something to pay whose period has ended, in deadline order. */
export function nextDue(prediction: TaxPredictionDto, today: string): { form: TaxFormDto; period: TaxPeriodDto } | null {
  for (const period of prediction.periods) {
    if (today <= period.end) continue
    const form = period.forms.find(f => !f.payment && f.estimated > 0)
    if (form) return { form, period }
  }
  return null
}

/**
 * The values the settings form starts from: the saved profile when it is for this regime, otherwise
 * the regime's defaults (a yes/no field the regime wants ticked is "true" there).
 */
export function initialValues(regime: TaxRegimeDto, profile: TaxProfileDto | null, countryCode: string): Record<string, string> {
  return profile && profile.countryCode === countryCode && profile.regimeId === regime.id
    ? { ...profile.values }
    : { ...regime.defaults }
}

/** "2026-07-15" -> "2026-07", the value of an `<input type="month">`. */
export function toMonthInput(date: string | null | undefined): string {
  return date ? date.slice(0, 7) : ''
}

/** "2026-07" -> "2026-07-01"; empty stays null. */
export function fromMonthInput(month: string): string | null {
  return /^\d{4}-\d{2}$/.test(month) ? `${month}-01` : null
}

/** Today in the viewer's time zone, as an ISO date. */
export function todayIso(now: Date = new Date()): string {
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000)
  return local.toISOString().slice(0, 10)
}

function round2(value: number): number {
  return Math.round(value * 100) / 100
}
