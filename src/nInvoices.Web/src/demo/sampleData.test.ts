import { describe, it, expect } from 'vitest'
import { buildSampleData, SAMPLE_FISCAL_IDS } from './sampleData'

// Mid-October 2026: the sample months are August and September
const today = new Date(2026, 9, 19)

describe('buildSampleData', () => {
  const data = buildSampleData(today)
  const customers = data.customers as { fiscalId: string; workDays: { date: string; projects: { hours: number }[] }[] }[]

  it('creates the sample customers under the ids that mark them as sample data', () => {
    expect(customers.map(c => c.fiscalId)).toEqual([...SAMPLE_FISCAL_IDS])
  })

  it('fills the two months before the current one, on weekdays only', () => {
    const dates = customers[0].workDays.map(d => d.date)

    expect(dates[0]).toBe('2026-08-03')
    expect(dates[dates.length - 1]).toBe('2026-09-30')
    expect(dates).toHaveLength(21 + 22)
    expect(dates.every(d => ![0, 6].includes(new Date(`${d}T12:00:00`).getDay()))).toBe(true)
  })

  it('never books more than a full day', () => {
    for (const day of customers.flatMap(c => c.workDays))
      expect(day.projects.reduce((sum, p) => sum + p.hours, 0)).toBeLessThanOrEqual(8)
  })

  it('brings the templates an invoice needs', () => {
    const shared = data.sharedTemplates as { invoiceTemplates: unknown[]; monthlyReportTemplates: unknown[] }

    expect(shared.invoiceTemplates).toHaveLength(1)
    expect(shared.monthlyReportTemplates).toHaveLength(1)
  })
})
