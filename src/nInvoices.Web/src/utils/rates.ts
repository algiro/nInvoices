import { RateType } from '@/types'
import type { RateDto, WorkDayDto } from '@/types'
import { formatMoney } from '@/utils/format'
import { billedHours, dayHours, FULL_DAY_HOURS } from '@/composables/useWorkMonth'

export function rateUnit(type: RateType | string): string {
  return type === RateType.Hourly ? 'per hour' : type === RateType.Monthly ? 'per month' : 'per day'
}

/** "€70.00 per hour", with the rate's name in front when it has one. */
export function rateLabel(rate: RateDto): string {
  const price = `${formatMoney(rate.price.amount, rate.price.currency)} ${rateUnit(rate.type)}`
  return rate.name ? `${rate.name} · ${price}` : price
}

/** A compact label for a calendar cell: the rate's name, else "€70/h". */
export function rateShortLabel(rate: RateDto): string {
  if (rate.name) return rate.name
  const unit = rate.type === RateType.Hourly ? 'h' : rate.type === RateType.Monthly ? 'mo' : 'day'
  return `${formatMoney(rate.price.amount, rate.price.currency)}/${unit}`
}

/** Daily and hourly rates can be chosen for a single worked day; a fixed monthly price can't. */
export function isDayRate(rate: RateDto): boolean {
  return rate.type === RateType.Daily || rate.type === RateType.Hourly
}

/**
 * What a worked day is billed at a rate. Same rule as the backend: an hourly rate bills the
 * day's hours, a daily rate bills the day (a partial day is hours / 8).
 */
export function dayAmount(rate: RateDto, workDay: WorkDayDto): number {
  if (rate.type === RateType.Hourly) return dayHours(workDay) * rate.price.amount
  if (rate.type === RateType.Daily) return (billedHours(workDay) / FULL_DAY_HOURS) * rate.price.amount
  return 0
}
