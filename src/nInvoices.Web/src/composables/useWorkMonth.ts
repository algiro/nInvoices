import { ref, computed, type Ref } from 'vue'
import { DayType } from '@/types'
import type { WorkDayDto } from '@/types'

export const FULL_DAY_HOURS = 8

/** A day's type as edited in the UI: the three persisted types plus "not billed" (no entry). */
export type DayKind = DayType | 'none'

export interface CalendarDay {
  date: string
  day: number
  isCurrentMonth: boolean
  isWeekend: boolean
}

export interface WorkMonthTotals {
  workedDays: number
  publicHolidays: number
  unpaidLeave: number
  partialDays: number
  totalHours: number
  effectiveDays: number
  notes: number
}

// Use local date components to avoid UTC-offset shifting when calling toISOString()
export function localDateString(date: Date): string {
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

export function isWorked(workDay: WorkDayDto | undefined): boolean {
  return !!workDay && (workDay.dayType ?? DayType.Worked) === DayType.Worked
}

/** Sum of the day's project rows. */
export function dayHours(workDay: WorkDayDto): number {
  return (workDay.projects ?? []).reduce((sum, p) => sum + (Number(p.hours) || 0), 0)
}

/**
 * Hours billed for a worked day: the sum of its project rows (the single source of truth).
 * A day with no rows at all is billed as a full day, matching the backend default.
 */
export function billedHours(workDay: WorkDayDto): number {
  return (workDay.projects ?? []).length > 0 ? dayHours(workDay) : FULL_DAY_HOURS
}

/**
 * Single model behind the time-entry UI: the month's work days (held by the caller, usually
 * the invoice form), the current selection, and every operation the calendar and inspector need.
 */
export function useWorkMonth(options: {
  workDays: Ref<WorkDayDto[]>
  year: Ref<number>
  month: Ref<number> // 1-12
  firstDayOfWeek: () => number // 0 = Sunday
  defaultProjectName?: () => string
}) {
  const { workDays, year, month, firstDayOfWeek } = options
  const defaultProject = () => options.defaultProjectName?.() ?? ''

  const selected = ref<string[]>([])
  const anchor = ref<string | null>(null)
  const cursor = ref<string | null>(null) // moving end of a keyboard range

  const byDate = computed(() => {
    const map = new Map<string, WorkDayDto>()
    workDays.value.forEach(wd => map.set(wd.date, wd))
    return map
  })

  function get(date: string): WorkDayDto | undefined {
    return byDate.value.get(date)
  }

  function kindOf(date: string): DayKind {
    const wd = get(date)
    return wd ? (wd.dayType ?? DayType.Worked) : 'none'
  }

  const monthDates = computed(() => {
    const last = new Date(year.value, month.value, 0).getDate()
    return Array.from({ length: last }, (_, i) => localDateString(new Date(year.value, month.value - 1, i + 1)))
  })

  const calendarDays = computed((): CalendarDay[] => {
    const first = new Date(year.value, month.value - 1, 1)
    const lastDay = new Date(year.value, month.value, 0).getDate()
    const lead = (first.getDay() - firstDayOfWeek() + 7) % 7
    const total = Math.ceil((lead + lastDay) / 7) * 7
    return Array.from({ length: total }, (_, i) => {
      const date = new Date(year.value, month.value - 1, 1 - lead + i)
      return {
        date: localDateString(date),
        day: date.getDate(),
        isCurrentMonth: date.getMonth() === month.value - 1,
        isWeekend: date.getDay() === 0 || date.getDay() === 6
      }
    })
  })

  const dayHeaders = computed(() => {
    const names = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
    const f = firstDayOfWeek()
    return [...names.slice(f), ...names.slice(0, f)]
  })

  // ---------- selection ----------

  function select(date: string, mode: 'replace' | 'toggle' | 'range' = 'replace') {
    cursor.value = date
    if (mode === 'range' && anchor.value) {
      selected.value = datesBetween(anchor.value, date)
      return
    }
    if (mode === 'toggle') {
      selected.value = selected.value.includes(date)
        ? selected.value.filter(d => d !== date)
        : [...selected.value, date]
      anchor.value = date
      return
    }
    selected.value = [date]
    anchor.value = date
  }

  function selectRange(from: string, to: string) {
    selected.value = datesBetween(from, to)
  }

  function clearSelection() {
    selected.value = []
    anchor.value = null
    cursor.value = null
  }

  /** Moves the cursor by a number of days inside the month; with extend, grows the range from the anchor. */
  function moveSelection(offset: number, extend = false) {
    const dates = monthDates.value
    const from = cursor.value ?? anchor.value
    const index = from ? dates.indexOf(from) : -1
    const next = dates[Math.min(dates.length - 1, Math.max(0, index < 0 ? 0 : index + offset))]
    if (extend && anchor.value) {
      selected.value = datesBetween(anchor.value, next)
      cursor.value = next
    } else {
      select(next)
    }
  }

  function datesBetween(a: string, b: string): string[] {
    const [lo, hi] = a <= b ? [a, b] : [b, a]
    return monthDates.value.filter(d => d >= lo && d <= hi)
  }

  const selectedDays = computed(() => [...selected.value].sort())

  // ---------- edits ----------

  function newWorkedDay(date: string, notes?: string): WorkDayDto {
    return { date, dayType: DayType.Worked, projects: [{ projectName: defaultProject(), hours: FULL_DAY_HOURS }], notes }
  }

  function setKind(dates: string[], kind: DayKind) {
    dates.forEach(date => {
      const index = workDays.value.findIndex(wd => wd.date === date)
      const existing = index >= 0 ? workDays.value[index] : undefined
      if (kind === 'none') {
        if (index >= 0) workDays.value.splice(index, 1)
        return
      }
      if (kind === DayType.Worked) {
        if (isWorked(existing)) return
        const day = newWorkedDay(date, existing?.notes)
        if (index >= 0) workDays.value.splice(index, 1, day)
        else workDays.value.push(day)
        return
      }
      const day: WorkDayDto = { date, dayType: kind, projects: [], notes: existing?.notes }
      if (index >= 0) workDays.value.splice(index, 1, day)
      else workDays.value.push(day)
    })
  }

  /**
   * Sets each day's total. One row takes the value directly; several rows are scaled
   * proportionally (to the nearest 0.25h) with rounding drift absorbed by the first row.
   * Days that aren't worked yet become worked days.
   */
  function setHours(dates: string[], hours: number) {
    const h = Math.max(0, Math.min(24, Number(hours) || 0))
    dates.forEach(date => {
      if (!isWorked(get(date))) setKind([date], DayType.Worked)
      const wd = get(date)!
      if (!wd.projects) wd.projects = []
      const rows = wd.projects // read back through the reactive proxy
      if (rows.length === 0) {
        rows.push({ projectName: defaultProject(), hours: h })
      } else if (rows.length === 1) {
        rows[0].hours = h
      } else {
        const current = dayHours(wd)
        if (current <= 0) {
          rows.forEach((r, i) => { r.hours = i === 0 ? h : 0 })
        } else {
          rows.forEach(r => { r.hours = Math.round((Number(r.hours) || 0) / current * h * 4) / 4 })
          rows[0].hours = Math.max(0, Number(rows[0].hours) + h - dayHours(wd))
        }
      }
    })
  }

  /** Puts every selected worked day on one project, keeping each day's total. */
  function setProject(dates: string[], projectName: string) {
    dates.forEach(date => {
      const wd = get(date)
      if (!isWorked(wd)) return
      wd!.projects = [{ projectName, hours: billedHours(wd!) }]
    })
  }

  function addAllocation(date: string) {
    const wd = get(date)
    if (!isWorked(wd)) return
    if (!wd!.projects) wd!.projects = []
    wd!.projects.push({ projectName: '', hours: 0 })
  }

  function removeAllocation(date: string, index: number) {
    get(date)?.projects?.splice(index, 1)
  }

  /** Sets the note on every selected day that has an entry ("not billed" days carry no note). */
  function setNote(dates: string[], note: string) {
    const value = note.trim().length > 0 ? note : undefined
    dates.forEach(date => {
      const wd = get(date)
      if (wd) wd.notes = value
    })
  }

  /** Marks every weekday that has no entry yet as a full worked day. Returns how many were added. */
  function fillWeekdays(): number {
    let added = 0
    monthDates.value.forEach(date => {
      const dow = new Date(date + 'T00:00:00').getDay()
      if (dow === 0 || dow === 6 || get(date)) return
      workDays.value.push(newWorkedDay(date))
      added++
    })
    return added
  }

  function clearMonth() {
    workDays.value.splice(0, workDays.value.length)
    clearSelection()
  }

  // ---------- totals ----------

  const workedDays = computed(() =>
    workDays.value.filter(wd => isWorked(wd)).sort((a, b) => a.date.localeCompare(b.date))
  )

  const totals = computed((): WorkMonthTotals => {
    const t: WorkMonthTotals = {
      workedDays: 0, publicHolidays: 0, unpaidLeave: 0, partialDays: 0, totalHours: 0, effectiveDays: 0, notes: 0
    }
    workDays.value.forEach(wd => {
      if (wd.notes) t.notes++
      const type = wd.dayType ?? DayType.Worked
      if (type === DayType.PublicHoliday) t.publicHolidays++
      else if (type === DayType.UnpaidLeave) t.unpaidLeave++
      else {
        const hours = billedHours(wd)
        t.workedDays++
        t.totalHours += dayHours(wd)
        t.effectiveDays += hours / FULL_DAY_HOURS
        if (hours !== FULL_DAY_HOURS) t.partialDays++
      }
    })
    return t
  })

  return {
    selected,
    selectedDays,
    anchor,
    calendarDays,
    dayHeaders,
    monthDates,
    workedDays,
    totals,
    get,
    kindOf,
    select,
    selectRange,
    clearSelection,
    moveSelection,
    setKind,
    setHours,
    setProject,
    addAllocation,
    removeAllocation,
    setNote,
    fillWeekdays,
    clearMonth
  }
}

export type WorkMonth = ReturnType<typeof useWorkMonth>
