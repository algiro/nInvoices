<template>
  <div class="month-calendar">
    <div
      ref="gridEl"
      class="calendar-grid"
      role="grid"
      tabindex="0"
      aria-label="Worked days calendar. Arrow keys move, Shift extends, W/H/L set the type, 1-8 set hours, Delete clears."
      aria-multiselectable="true"
      @keydown="onKeydown"
    >
      <div v-for="name in dayHeaders" :key="name" class="day-header" role="columnheader">{{ name }}</div>

      <template v-for="day in calendarDays" :key="day.date">
        <div v-if="!day.isCurrentMonth" class="cell outside" aria-hidden="true">{{ day.day }}</div>
        <div
          v-else
          class="cell"
          role="gridcell"
          :aria-selected="selected.includes(day.date)"
          :title="cellTitle(day.date)"
          :class="cellClasses(day)"
          :style="partialStyle(day.date)"
          @pointerdown="onPointerDown($event, day.date)"
          @pointerenter="onPointerEnter(day.date)"
        >
          <span class="cell-top">
            <span class="cell-num">{{ day.day }}</span>
            <span v-if="projectCount(day.date) > 1" class="split-badge" title="Split across projects">×{{ projectCount(day.date) }}</span>
            <svg v-if="get(day.date)?.notes" class="note-icon" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" aria-label="Has a note">
              <path d="M3 2.5h10v8l-3 3H3z M10 13.5v-3h3M5.5 6h5M5.5 8.5h3" />
            </svg>
          </span>

          <template v-if="kindOf(day.date) === DayType.Worked">
            <span class="cell-hours">{{ formatHours(billedHours(get(day.date)!)) }}</span>
            <span class="cell-project">{{ projectLabel(day.date) }}</span>
          </template>
          <span v-else-if="kindOf(day.date) === DayType.PublicHoliday" class="cell-label">Holiday</span>
          <span v-else-if="kindOf(day.date) === DayType.UnpaidLeave" class="cell-label">Unpaid leave</span>
          <span v-else-if="!day.isWeekend" class="cell-label muted">Not billed</span>
        </div>
      </template>
    </div>

    <div class="legend">
      <span><i class="swatch worked"></i>Worked</span>
      <span><i class="swatch partial"></i>Partial day (fill = share of 8h)</span>
      <span><i class="swatch holiday"></i>Public holiday</span>
      <span><i class="swatch leave"></i>Unpaid leave</span>
      <span class="legend-hint">Drag or Shift+click to select several days</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { DayType } from '@/types'
import { billedHours, localDateString, FULL_DAY_HOURS, type CalendarDay, type WorkMonth } from '@/composables/useWorkMonth'

const props = defineProps<{ month: WorkMonth }>()

const { calendarDays, dayHeaders, selected, anchor, get, kindOf, select, selectRange, clearSelection, moveSelection, setKind, setHours, selectedDays } = props.month

const gridEl = ref<HTMLElement | null>(null)
const dragging = ref(false)
const today = localDateString(new Date())

function projectCount(date: string): number {
  return (get(date)?.projects ?? []).filter(p => p.projectName.trim() || Number(p.hours) > 0).length
}

function projectLabel(date: string): string {
  const rows = get(date)?.projects ?? []
  if (projectCount(date) > 1) return 'Split'
  return rows[0]?.projectName?.trim() || ''
}

function formatHours(h: number): string {
  return `${Math.round(h * 100) / 100}h`
}

function cellClasses(day: CalendarDay) {
  const kind = kindOf(day.date)
  const wd = get(day.date)
  const hours = wd && kind === DayType.Worked ? billedHours(wd) : null
  return {
    weekend: day.isWeekend,
    today: day.date === today,
    selected: selected.value.includes(day.date),
    worked: kind === DayType.Worked,
    partial: hours !== null && hours !== FULL_DAY_HOURS,
    'no-hours': hours !== null && hours <= 0,
    holiday: kind === DayType.PublicHoliday,
    leave: kind === DayType.UnpaidLeave
  }
}

function partialStyle(date: string) {
  const wd = get(date)
  if (!wd || kindOf(date) !== DayType.Worked) return undefined
  return { '--fill': `${Math.min(100, (billedHours(wd) / FULL_DAY_HOURS) * 100)}%` }
}

function cellTitle(date: string): string {
  const d = new Date(date + 'T00:00:00')
  const label = d.toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long' })
  const note = get(date)?.notes
  return note ? `${label} — ${note}` : label
}

// ---------- pointer selection ----------

function onPointerDown(event: PointerEvent, date: string) {
  if (event.button !== 0) return
  event.preventDefault() // no text selection while dragging
  gridEl.value?.focus()
  if (event.shiftKey && anchor.value) {
    select(date, 'range')
  } else if (event.ctrlKey || event.metaKey) {
    select(date, 'toggle')
  } else {
    select(date)
    dragging.value = true
  }
}

function onPointerEnter(date: string) {
  if (dragging.value && anchor.value) selectRange(anchor.value, date)
}

function stopDragging() {
  dragging.value = false
}

onMounted(() => window.addEventListener('pointerup', stopDragging))
onBeforeUnmount(() => window.removeEventListener('pointerup', stopDragging))

// ---------- keyboard ----------

const arrowOffsets: Record<string, number> = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 }

function onKeydown(event: KeyboardEvent) {
  if (event.ctrlKey || event.metaKey || event.altKey) return

  if (event.key in arrowOffsets) {
    moveSelection(arrowOffsets[event.key], event.shiftKey)
    event.preventDefault()
    return
  }
  if (event.key === 'Escape') {
    clearSelection()
    return
  }

  const days = selectedDays.value
  if (days.length === 0) return
  const key = event.key.toLowerCase()

  if (key === 'w') setKind(days, DayType.Worked)
  else if (key === 'h') setKind(days, DayType.PublicHoliday)
  else if (key === 'l') setKind(days, DayType.UnpaidLeave)
  else if (key === 'delete' || key === 'backspace') setKind(days, 'none')
  else if (/^[1-8]$/.test(key)) setHours(days, Number(key))
  else return

  event.preventDefault()
}
</script>

<style scoped>
.month-calendar {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  min-width: 0;
}

.calendar-grid {
  display: grid;
  grid-template-columns: repeat(7, minmax(0, 1fr));
  gap: 0.375rem;
  user-select: none;
  touch-action: none;
  border-radius: 0.5rem;
}

.calendar-grid:focus-visible {
  outline: 2px solid var(--color-primary);
  outline-offset: 4px;
}

.day-header {
  padding: 0 0.375rem 0.125rem;
  font-size: 0.7rem;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.cell {
  position: relative;
  min-height: 5rem;
  padding: 0.375rem 0.5rem;
  display: grid;
  grid-template-rows: auto 1fr auto;
  gap: 0.125rem;
  border: 1px solid var(--color-border);
  border-radius: 0.5rem;
  background: var(--color-surface);
  cursor: pointer;
  overflow: hidden;
  color: var(--color-text);
}

.cell:hover {
  border-color: var(--color-primary-line);
}

.cell.outside {
  cursor: default;
  border-color: transparent;
  background: transparent;
  color: var(--color-text-subtle);
  opacity: 0.55;
  font-size: 0.8rem;
}

.cell.weekend {
  background: var(--color-surface-muted);
}

.cell-top {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.cell-num {
  flex: 1;
  font-size: 0.8rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.cell.today .cell-num {
  color: var(--color-primary);
}

.cell.today .cell-num::after {
  content: ' · today';
  font-size: 0.65rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.split-badge {
  font-size: 0.65rem;
  font-weight: 600;
  line-height: 1;
  padding: 0.1rem 0.2rem;
  border: 1px solid var(--color-border-strong);
  border-radius: 0.2rem;
  color: var(--color-text-secondary);
}

.note-icon {
  width: 0.85rem;
  height: 0.85rem;
  color: var(--color-text-secondary);
  flex: none;
}

.cell-hours {
  align-self: end;
  font-size: 1rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.cell-project {
  font-size: 0.7rem;
  color: var(--color-text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  min-height: 1em;
}

.cell-label {
  align-self: end;
  font-size: 0.75rem;
  font-weight: 600;
}

.cell-label.muted {
  color: var(--color-text-subtle);
  font-weight: 400;
}

.cell.worked {
  background: var(--color-success-soft);
  border-color: var(--color-success-line);
}

.cell.worked .cell-hours {
  color: var(--color-success);
}

.cell.worked.partial {
  background: linear-gradient(to top, var(--color-success-soft) var(--fill, 50%), var(--color-surface) var(--fill, 50%));
}

.cell.worked.partial .cell-hours {
  color: var(--color-warning);
}

.cell.worked.no-hours {
  border-color: var(--color-danger);
}

.cell.holiday {
  background: var(--color-warning-soft);
  border-color: var(--color-warning-line);
}

.cell.holiday .cell-label {
  color: var(--color-warning);
}

.cell.leave {
  background: var(--color-danger-soft);
  border-color: var(--color-danger-line);
}

.cell.leave .cell-label {
  color: var(--color-danger);
}

.cell.selected {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 2px var(--color-primary);
}

.legend {
  display: flex;
  flex-wrap: wrap;
  gap: 0.375rem 1rem;
  font-size: 0.8rem;
  color: var(--color-text-muted);
}

.legend span {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
}

.legend-hint {
  margin-left: auto;
}

.swatch {
  width: 0.8rem;
  height: 0.8rem;
  border-radius: 0.2rem;
  border: 1px solid;
  display: inline-block;
}

.swatch.worked { background: var(--color-success-soft); border-color: var(--color-success-line); }
.swatch.partial { background: linear-gradient(to top, var(--color-success-soft) 50%, var(--color-surface) 50%); border-color: var(--color-success-line); }
.swatch.holiday { background: var(--color-warning-soft); border-color: var(--color-warning-line); }
.swatch.leave { background: var(--color-danger-soft); border-color: var(--color-danger-line); }

@media (max-width: 640px) {
  .cell { min-height: 3.75rem; padding: 0.25rem 0.3rem; }
  .cell-project, .cell.today .cell-num::after { display: none; }
  .cell-hours { font-size: 0.85rem; }
  .legend-hint { margin-left: 0; }
}
</style>
