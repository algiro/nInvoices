// @vitest-environment jsdom
import { describe, it, expect } from 'vitest'
import { ref } from 'vue'
import { mount } from '@vue/test-utils'
import { i18n } from '@/i18n'
import { useWorkMonth } from '@/composables/useWorkMonth'
import type { WorkDayDto } from '@/types'
import MonthCalendar from './MonthCalendar.vue'

// September 2026 starts on a Tuesday
function calendar() {
  const month = useWorkMonth({
    workDays: ref<WorkDayDto[]>([]),
    year: ref(2026),
    month: ref(9),
    firstDayOfWeek: () => 1,
    defaultProjectName: () => ''
  })
  const wrapper = mount(MonthCalendar, { props: { month }, global: { plugins: [i18n] } })
  const cell = (day: number) => wrapper.findAll('.cell:not(.outside)')[day - 1]
  return { month, cell }
}

const pointer = (type: string, buttons: number) => new MouseEvent(type, { bubbles: true, button: 0, buttons })

describe('MonthCalendar pointer selection', () => {
  it('selects one day on a click', async () => {
    const { month, cell } = calendar()

    cell(1).element.dispatchEvent(pointer('pointerdown', 1))
    window.dispatchEvent(pointer('pointerup', 0))
    cell(8).element.dispatchEvent(pointer('pointerenter', 0))

    expect(month.selected.value).toEqual(['2026-09-01'])
  })

  it('selects the days dragged over while the button is held', () => {
    const { month, cell } = calendar()

    cell(1).element.dispatchEvent(pointer('pointerdown', 1))
    cell(3).element.dispatchEvent(pointer('pointerenter', 1))

    expect(month.selected.value).toEqual(['2026-09-01', '2026-09-02', '2026-09-03'])
  })

  it('stops growing the selection when the release was never seen', () => {
    const { month, cell } = calendar()

    // The pointerup is lost (released outside the window, over a screen recorder...), then the mouse moves on
    cell(1).element.dispatchEvent(pointer('pointerdown', 1))
    cell(8).element.dispatchEvent(pointer('pointerenter', 0))
    cell(9).element.dispatchEvent(pointer('pointerenter', 0))

    expect(month.selected.value).toEqual(['2026-09-01'])
  })

  it('ends the drag when the pointer is cancelled', () => {
    const { month, cell } = calendar()

    cell(1).element.dispatchEvent(pointer('pointerdown', 1))
    window.dispatchEvent(pointer('pointercancel', 1))
    cell(4).element.dispatchEvent(pointer('pointerenter', 1))

    expect(month.selected.value).toEqual(['2026-09-01'])
  })
})
