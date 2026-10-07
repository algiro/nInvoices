import { describe, it, expect } from 'vitest'
import { RateType, type RateDto, type WorkDayDto } from '@/types'
import { dayAmount, isDayRate } from './rates'

function rate(type: RateType, amount: number): RateDto {
  return { id: 1, customerId: 1, type, price: { amount, currency: 'EUR' }, isActive: true, createdAt: '2026-01-01' }
}

const day = (...hours: number[]): WorkDayDto => ({
  date: '2026-09-01',
  projects: hours.map((h, i) => ({ projectName: `P${i}`, hours: h }))
})

// The same rule as the backend's InvoiceCalculator
describe('dayAmount', () => {
  it('bills an hourly rate by the hours on the day', () => {
    expect(dayAmount(rate(RateType.Hourly, 80), day(3, 2.5))).toBe(440)
  })

  it('bills a daily rate per day, a partial day as hours over eight', () => {
    expect(dayAmount(rate(RateType.Daily, 400), day(8))).toBe(400)
    expect(dayAmount(rate(RateType.Daily, 400), day(4))).toBe(200)
    expect(dayAmount(rate(RateType.Daily, 400), day())).toBe(400) // no rows: a full day
  })

  it('bills nothing per day at a fixed monthly price, which cannot be a day rate', () => {
    expect(dayAmount(rate(RateType.Monthly, 9000), day(8))).toBe(0)
    expect(isDayRate(rate(RateType.Monthly, 9000))).toBe(false)
  })
})
