<template>
  <div class="timesheet">
    <div v-if="selectedDays.length > 1" class="bulk-bar">
      <strong>{{ selectedDays.length }} days selected</strong>
      <span class="spacer"></span>
      <button type="button" class="bulk-btn" @click="setKind(selectedDays, DayType.Worked)">Worked</button>
      <button type="button" class="bulk-btn" @click="setKind(selectedDays, DayType.PublicHoliday)">Holiday</button>
      <button type="button" class="bulk-btn" @click="setKind(selectedDays, DayType.UnpaidLeave)">Leave</button>
      <button type="button" class="bulk-btn" @click="setHours(selectedDays, 8)">8h</button>
      <button type="button" class="bulk-btn" @click="setHours(selectedDays, 4)">4h</button>
      <button type="button" class="bulk-btn ghost" @click="clearSelection()">Clear selection</button>
    </div>

    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th scope="col">Day</th>
            <th scope="col">Type</th>
            <th scope="col" class="num">Hours</th>
            <th scope="col">Project</th>
            <th scope="col">Note</th>
            <th v-if="showAmount" scope="col" class="num">Amount</th>
          </tr>
        </thead>
        <tbody>
          <template v-for="row in rows" :key="row.date">
            <!-- weekend days without an entry collapse into one thin row per weekend -->
            <tr v-if="row.collapsed" class="weekend-row">
              <td class="day-cell" @pointerdown="onDayPointerDown($event, row.date)">
                <span class="day-num">{{ row.day }}</span><span class="day-name">{{ row.dayName }}</span>
              </td>
              <td :colspan="showAmount ? 5 : 4">
                {{ row.label }} ·
                <button
                  v-for="d in row.markable"
                  :key="d.date"
                  type="button"
                  class="link-btn"
                  @click="setKind([d.date], DayType.Worked)"
                >
                  mark {{ d.label }} as worked
                </button>
              </td>
            </tr>

            <tr v-else :class="{ selected: selected.includes(row.date), weekend: row.isWeekend }">
              <td
                class="day-cell"
                :title="'Click to select · Shift+click for a range · Ctrl+click to add'"
                @pointerdown="onDayPointerDown($event, row.date)"
              >
                <span class="day-num">{{ row.day }}</span><span class="day-name">{{ row.dayName }}</span>
              </td>

              <td>
                <select
                  :id="`ts-type-${row.date}`"
                  class="input type-select"
                  :class="kindClass(row.date)"
                  :value="String(kindOf(row.date))"
                  :aria-label="`Type for ${row.label}`"
                  @change="onKindChange(row.date, ($event.target as HTMLSelectElement).value)"
                >
                  <option :value="String(DayType.Worked)">Worked</option>
                  <option :value="String(DayType.PublicHoliday)">Public holiday</option>
                  <option :value="String(DayType.UnpaidLeave)">Unpaid leave</option>
                  <option value="none">Not billed</option>
                </select>
              </td>

              <td class="num">
                <input
                  v-if="kindOf(row.date) === DayType.Worked"
                  :id="`ts-hours-${row.date}`"
                  type="number"
                  step="0.25"
                  min="0"
                  max="24"
                  class="input hours-input"
                  :class="{ invalid: hoursOf(row.date) <= 0 }"
                  :value="hoursOf(row.date)"
                  :readonly="isSplit(row.date)"
                  :title="isSplit(row.date) ? 'Split across projects: edit the hours in the day panel' : ''"
                  :aria-label="`Hours for ${row.label}`"
                  @change="setHours([row.date], Number(($event.target as HTMLInputElement).value))"
                />
                <span v-else class="dash">—</span>
              </td>

              <td>
                <template v-if="kindOf(row.date) === DayType.Worked">
                  <button
                    v-if="isSplit(row.date)"
                    type="button"
                    class="split-chip"
                    title="Edit the split in the day panel"
                    @click="select(row.date)"
                  >
                    {{ splitLabel(row.date) }}
                  </button>
                  <input
                    v-else
                    :id="`ts-project-${row.date}`"
                    type="text"
                    list="timesheet-project-suggestions"
                    class="input"
                    placeholder="Project (optional)"
                    :value="get(row.date)?.projects?.[0]?.projectName ?? ''"
                    :aria-label="`Project for ${row.label}`"
                    @change="setProject([row.date], ($event.target as HTMLInputElement).value.trim())"
                  />
                </template>
                <span v-else class="dash">—</span>
              </td>

              <td>
                <input
                  v-if="kindOf(row.date) !== 'none'"
                  :id="`ts-note-${row.date}`"
                  type="text"
                  maxlength="500"
                  class="input"
                  placeholder="Add a note…"
                  :value="get(row.date)?.notes ?? ''"
                  :aria-label="`Note for ${row.label}`"
                  @input="setNote([row.date], ($event.target as HTMLInputElement).value)"
                />
                <span v-else class="dash">—</span>
              </td>

              <td v-if="showAmount" class="num amount">{{ amountOf(row.date) }}</td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>

    <datalist id="timesheet-project-suggestions">
      <option v-for="p in projectSuggestions" :key="p" :value="p" />
    </datalist>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { DayType, RateType } from '@/types'
import { billedHours, dayHours, FULL_DAY_HOURS, type DayKind, type WorkMonth } from '@/composables/useWorkMonth'

const props = defineProps<{
  month: WorkMonth
  rateType?: RateType | null
  rateAmount?: number | null
  currency?: string | null
  projectSuggestions: string[]
}>()

const {
  monthDates, selected, selectedDays, anchor, get, kindOf, select, clearSelection,
  setKind, setHours, setProject, setNote
} = props.month

const dayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']

interface Row {
  date: string
  day: number
  dayName: string
  label: string
  isWeekend: boolean
  collapsed: boolean
  markable: { date: string; label: string }[]
}

// One row per day; a weekend (Sat+Sun) with no entries collapses into a single thin row
const rows = computed((): Row[] => {
  const result: Row[] = []
  const dates = monthDates.value
  for (let i = 0; i < dates.length; i++) {
    const date = dates[i]
    const d = new Date(date + 'T00:00:00')
    const dow = d.getDay()
    const isWeekend = dow === 0 || dow === 6
    const base = {
      date,
      day: d.getDate(),
      dayName: dayNames[dow],
      label: d.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' }),
      isWeekend
    }
    if (isWeekend && !get(date)) {
      // a Sunday already folded into the preceding Saturday row
      if (dow === 0 && i > 0 && result[result.length - 1]?.collapsed) continue
      const next = dates[i + 1]
      const foldSunday = dow === 6 && next && !get(next)
      const markable = [{ date, label: `${dayNames[dow]} ${d.getDate()}` }]
      if (foldSunday) markable.push({ date: next, label: `Sun ${d.getDate() + 1}` })
      result.push({ ...base, collapsed: true, markable, label: foldSunday ? 'Weekend' : `${dayNames[dow]}, not billed` })
      continue
    }
    result.push({ ...base, collapsed: false, markable: [] })
  }
  return result
})

const showAmount = computed(
  () => !!props.rateAmount && (props.rateType === RateType.Daily || props.rateType === RateType.Hourly)
)

function hoursOf(date: string): number {
  const wd = get(date)
  return wd ? billedHours(wd) : 0
}

function isSplit(date: string): boolean {
  return (get(date)?.projects ?? []).length > 1
}

function splitLabel(date: string): string {
  return (get(date)?.projects ?? [])
    .map(p => `${p.projectName || 'Unnamed'} ${Number(p.hours) || 0}h`)
    .join(' · ')
}

function amountOf(date: string): string {
  const wd = get(date)
  if (!wd || kindOf(date) !== DayType.Worked || !props.rateAmount) return ''
  const value = props.rateType === RateType.Hourly
    ? dayHours(wd) * props.rateAmount
    : (billedHours(wd) / FULL_DAY_HOURS) * props.rateAmount
  return `${value.toFixed(2)} ${props.currency ?? ''}`.trim()
}

function kindClass(date: string): string {
  const kind = kindOf(date)
  if (kind === DayType.Worked) return 'worked'
  if (kind === DayType.PublicHoliday) return 'holiday'
  if (kind === DayType.UnpaidLeave) return 'leave'
  return 'none'
}

function onKindChange(date: string, value: string) {
  const kind: DayKind = value === 'none' ? 'none' : (Number(value) as DayType)
  setKind([date], kind)
}

function onDayPointerDown(event: PointerEvent, date: string) {
  if (event.button !== 0) return
  if (event.shiftKey && anchor.value) {
    event.preventDefault()
    select(date, 'range')
  } else if (event.ctrlKey || event.metaKey) {
    select(date, 'toggle')
  } else {
    select(date)
  }
}
</script>

<style scoped>
.timesheet {
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
  min-width: 0;
}

.bulk-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.375rem;
  padding: 0.4rem 0.625rem;
  border: 1px solid #bfd3fb;
  border-radius: 0.5rem;
  background: #eff6ff;
  font-size: 0.85rem;
}

.bulk-bar strong {
  color: #1d4ed8;
}

.spacer {
  flex: 1;
}

.bulk-btn {
  padding: 0.2rem 0.6rem;
  border: 1px solid #d1d5db;
  border-radius: 0.375rem;
  background: #ffffff;
  color: #374151;
  font: inherit;
  font-size: 0.8rem;
  line-height: 1.3;
  cursor: pointer;
}

.bulk-btn:hover {
  border-color: #93b4f5;
  color: #1d4ed8;
}

.bulk-btn.ghost {
  border-color: transparent;
  background: transparent;
}

.table-wrap {
  max-height: 34rem;
  overflow: auto;
  border: 1px solid #e5e7eb;
  border-radius: 0.5rem;
  background: #ffffff;
}

table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.85rem;
}

th {
  position: sticky;
  top: 0;
  z-index: 1;
  padding: 0.5rem 0.5rem;
  background: #f9fafb;
  border-bottom: 1px solid #e5e7eb;
  text-align: left;
  font-size: 0.7rem;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: #6b7280;
  white-space: nowrap;
}

td {
  padding: 0.25rem 0.5rem;
  border-bottom: 1px solid #f1f2f4;
  vertical-align: middle;
}

.num {
  text-align: right;
}

tr.selected td {
  background: #eff6ff;
}

tr.weekend td {
  background: #fafafa;
}

tr.selected.weekend td {
  background: #eff6ff;
}

.day-cell {
  width: 5.5rem;
  white-space: nowrap;
  cursor: pointer;
  user-select: none;
}

.day-num {
  display: inline-block;
  width: 1.75rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.day-name {
  color: #6b7280;
}

.weekend-row td {
  padding-block: 0.2rem;
  background: #fafafa;
  color: #9ca3af;
  font-size: 0.78rem;
}

.input {
  width: 100%;
  min-width: 0;
  padding: 0.25rem 0.45rem;
  border: 1px solid #e5e7eb;
  border-radius: 0.3rem;
  background: #ffffff;
  font: inherit;
  font-size: 0.85rem;
}

.input:hover {
  border-color: #d1d5db;
}

.input:focus {
  outline: none;
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.12);
}

.type-select {
  width: 8.75rem;
  font-weight: 500;
}

.type-select.worked { color: #15803d; }
.type-select.holiday { color: #b45309; }
.type-select.leave { color: #b91c1c; }
.type-select.none { color: #6b7280; }

.hours-input {
  width: 4.75rem;
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.hours-input[readonly] {
  background: #f9fafb;
  color: #6b7280;
}

.hours-input.invalid {
  border-color: #dc2626;
}

.split-chip {
  max-width: 16rem;
  padding: 0.2rem 0.5rem;
  border: 1px dashed #cbd5e1;
  border-radius: 0.3rem;
  background: #ffffff;
  color: #374151;
  font: inherit;
  font-size: 0.78rem;
  line-height: 1.3;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  cursor: pointer;
}

.split-chip:hover {
  border-color: #2563eb;
}

.amount {
  white-space: nowrap;
  font-variant-numeric: tabular-nums;
  color: #374151;
}

.dash {
  color: #d1d5db;
}

.link-btn {
  padding: 0;
  margin-right: 0.5rem;
  border: 0;
  background: none;
  color: #2563eb;
  font: inherit;
  font-size: 0.78rem;
  cursor: pointer;
}

.link-btn:hover {
  text-decoration: underline;
}

@media (max-width: 640px) {
  table {
    min-width: 40rem;
  }
}
</style>
