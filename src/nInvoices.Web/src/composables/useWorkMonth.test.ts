import { describe, it, expect } from 'vitest'
import { ref } from 'vue'
import { DayType, type WorkDayDto } from '@/types'
import { useWorkMonth, billedHours, dayHours } from './useWorkMonth'

// September 2026 starts on a Tuesday and has 22 weekdays
function month(days: WorkDayDto[] = [], firstDayOfWeek = 1) {
  const workDays = ref<WorkDayDto[]>(days)
  const model = useWorkMonth({
    workDays,
    year: ref(2026),
    month: ref(9),
    firstDayOfWeek: () => firstDayOfWeek,
    defaultProjectName: () => 'Build'
  })
  return { workDays, model }
}

describe('day hours', () => {
  it('bills the project rows, or a full day when there are none', () => {
    expect(billedHours({ date: '2026-09-01', projects: [{ projectName: 'A', hours: 3 }, { projectName: 'B', hours: 2.5 }] })).toBe(5.5)
    expect(billedHours({ date: '2026-09-01', projects: [] })).toBe(8)
    expect(dayHours({ date: '2026-09-01' })).toBe(0)
  })
})

describe('useWorkMonth calendar', () => {
  it('pads the grid to whole weeks starting on the chosen weekday', () => {
    const { model } = month([], 1)

    expect(model.calendarDays.value).toHaveLength(35)
    expect(model.calendarDays.value[0]).toMatchObject({ date: '2026-08-31', isCurrentMonth: false })
    expect(model.calendarDays.value[1]).toMatchObject({ date: '2026-09-01', day: 1, isCurrentMonth: true, isWeekend: false })
    expect(model.dayHeaders.value[0]).toBe('Mon')
    expect(model.monthDates.value).toHaveLength(30)
  })

  it('starts on Sunday when that is the first day of the week', () => {
    const { model } = month([], 0)

    expect(model.calendarDays.value[0].date).toBe('2026-08-30')
    expect(model.dayHeaders.value[0]).toBe('Sun')
  })
})

describe('useWorkMonth selection', () => {
  it('selects a range from the anchor in either direction, within the month', () => {
    const { model } = month()

    model.select('2026-09-10')
    model.select('2026-09-07', 'range')

    expect(model.selectedDays.value).toEqual(['2026-09-07', '2026-09-08', '2026-09-09', '2026-09-10'])
  })

  it('toggles single days in and out', () => {
    const { model } = month()

    model.select('2026-09-01')
    model.select('2026-09-03', 'toggle')
    model.select('2026-09-01', 'toggle')

    expect(model.selectedDays.value).toEqual(['2026-09-03'])
  })
})

describe('useWorkMonth edits', () => {
  it('marks days worked with a full day on the default project, and removes them again', () => {
    const { model, workDays } = month()

    model.setKind(['2026-09-01'], DayType.Worked)
    expect(workDays.value).toEqual([
      { date: '2026-09-01', dayType: DayType.Worked, projects: [{ projectName: 'Build', hours: 8 }], notes: undefined }
    ])

    model.setKind(['2026-09-01'], 'none')
    expect(workDays.value).toEqual([])
  })

  it('keeps the note when a day changes type', () => {
    const { model } = month([{ date: '2026-09-01', dayType: DayType.Worked, projects: [], notes: 'kickoff' }])

    model.setKind(['2026-09-01'], DayType.PublicHoliday)

    expect(model.get('2026-09-01')).toMatchObject({ dayType: DayType.PublicHoliday, notes: 'kickoff' })
  })

  it('scales several project rows to a new total, in quarter hours, without drift', () => {
    const { model } = month([{
      date: '2026-09-01',
      dayType: DayType.Worked,
      projects: [{ projectName: 'A', hours: 5 }, { projectName: 'B', hours: 3 }]
    }])

    model.setHours(['2026-09-01'], 6)

    const rows = model.get('2026-09-01')!.projects!
    expect(rows.map(r => r.hours)).toEqual([3.75, 2.25])
    expect(dayHours(model.get('2026-09-01')!)).toBe(6)
  })

  it('fills the weekdays that have no entry, leaving the others alone', () => {
    const { model, workDays } = month([{ date: '2026-09-01', dayType: DayType.PublicHoliday, projects: [] }])

    expect(model.fillWeekdays()).toBe(21)
    expect(workDays.value).toHaveLength(22)
    expect(model.get('2026-09-01')!.dayType).toBe(DayType.PublicHoliday)
    expect(model.get('2026-09-05')).toBeUndefined() // a Saturday
  })
})

describe('useWorkMonth totals', () => {
  it('counts days by type and partial days in effective days', () => {
    const { model } = month([
      { date: '2026-09-01', dayType: DayType.Worked, projects: [{ projectName: 'A', hours: 8 }], notes: 'x' },
      { date: '2026-09-02', dayType: DayType.Worked, projects: [{ projectName: 'A', hours: 4 }] },
      { date: '2026-09-03', dayType: DayType.PublicHoliday, projects: [] },
      { date: '2026-09-04', dayType: DayType.UnpaidLeave, projects: [] }
    ])

    expect(model.totals.value).toEqual({
      workedDays: 2, publicHolidays: 1, unpaidLeave: 1, partialDays: 1, totalHours: 12, effectiveDays: 1.5, notes: 1
    })
  })
})
