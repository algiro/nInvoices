import { describe, expect, it } from 'vitest'
import type { TaxFormDto, TaxPeriodDto, TaxPredictionDto, TaxRegimeDto } from '@/types'
import { formStatus, fromMonthInput, initialValues, nextDue, toMonthInput, yearTotals } from './taxPrediction'

function form(code: string, estimated: number, paid?: number): TaxFormDto {
  return {
    code,
    name: `Modelo ${code}`,
    deadline: '2026-04-20',
    estimated,
    payment: paid === undefined ? null : { id: 1, year: 2026, period: 1, form: code, amount: paid, currency: 'EUR', paidOn: '2026-04-20' },
    lines: []
  }
}

const q1: TaxPeriodDto = { period: 1, name: 'Q1', start: '2026-01-01', end: '2026-03-31', forms: [form('130', 2625.61, 2625), form('420', 210)] }
const q2: TaxPeriodDto = { period: 2, name: 'Q2', start: '2026-04-01', end: '2026-06-30', forms: [{ ...form('130', 2243.56), deadline: '2026-07-20' }] }
const prediction: TaxPredictionDto = { year: 2026, isSetUp: true, countryCode: 'ES', currency: 'EUR', periods: [q1, q2], warnings: [] }

describe('formStatus', () => {
  it('is paid once a payment is recorded, whatever the date', () => {
    expect(formStatus(q1.forms[0], q1, '2026-12-31')).toEqual({ label: 'Paid', tone: 'success' })
  })

  it('is in progress while the quarter runs, due until the deadline, then overdue', () => {
    const igic = q1.forms[1]
    expect(formStatus(igic, q1, '2026-03-31').label).toBe('Quarter in progress')
    expect(formStatus(igic, q1, '2026-04-20').label).toBe('Due')
    expect(formStatus(igic, q1, '2026-04-21').label).toBe('Overdue')
  })
})

describe('yearTotals', () => {
  it('adds up the estimates and the recorded payments', () => {
    expect(yearTotals(prediction)).toEqual({ estimated: 5079.17, paid: 2625 })
  })
})

describe('nextDue', () => {
  it('is the first unpaid form of a quarter that has ended', () => {
    expect(nextDue(prediction, '2026-04-10')?.form.code).toBe('420')
    expect(nextDue(prediction, '2026-03-15')).toBeNull()
  })
})

describe('initialValues', () => {
  const regime: TaxRegimeDto = { id: 'eds', name: 'EDS', help: null, fields: [], defaults: { difficultExpenses: 'true', deductWithholdings: 'true' } }

  it('starts from the defaults before the first save', () => {
    expect(initialValues(regime, null, 'ES')).toEqual({ difficultExpenses: 'true', deductWithholdings: 'true' })
  })

  it('keeps what was saved, unticked fields included', () => {
    const profile = { countryCode: 'ES', regimeId: 'eds', values: { territory: 'canarias', difficultExpenses: 'true' } }
    expect(initialValues(regime, profile, 'ES')).toEqual({ territory: 'canarias', difficultExpenses: 'true' })
  })

  it('uses the defaults for another country or regime than the saved one', () => {
    const profile = { countryCode: 'PT', regimeId: 'eds', values: {} }
    expect(initialValues(regime, profile, 'ES')).toEqual(regime.defaults)
  })
})

describe('month inputs', () => {
  it('converts between dates and <input type="month"> values', () => {
    expect(toMonthInput('2026-07-01')).toBe('2026-07')
    expect(toMonthInput(null)).toBe('')
    expect(fromMonthInput('2026-07')).toBe('2026-07-01')
    expect(fromMonthInput('')).toBeNull()
  })
})
