<template>
  <div class="timesheet">
    <div v-if="selectedDays.length > 1" class="bulk-bar">
      <strong>{{ t('time.daysSelected', { n: selectedDays.length }) }}</strong>
      <span class="spacer"></span>
      <button type="button" class="bulk-btn" @click="setKind(selectedDays, DayType.Worked)">{{ t('time.kinds.worked') }}</button>
      <button type="button" class="bulk-btn" @click="setKind(selectedDays, DayType.PublicHoliday)">{{ t('time.kinds.holiday') }}</button>
      <button type="button" class="bulk-btn" @click="setKind(selectedDays, DayType.UnpaidLeave)">{{ t('time.kinds.leave') }}</button>
      <button type="button" class="bulk-btn" @click="setHours(selectedDays, 8)">8h</button>
      <button type="button" class="bulk-btn" @click="setHours(selectedDays, 4)">4h</button>
      <button type="button" class="bulk-btn ghost" @click="clearSelection()">{{ t('invoices.bulk.clearSelection') }}</button>
    </div>

    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th scope="col">{{ t('time.day') }}</th>
            <th scope="col">{{ t('common.type') }}</th>
            <th scope="col" class="num">{{ t('invoiceWizard.hours') }}</th>
            <th scope="col">{{ t('projects.project') }}</th>
            <th v-if="showRate" scope="col">{{ t('rates.rate') }}</th>
            <th scope="col">{{ t('time.note') }}</th>
            <th v-if="showAmount" scope="col" class="num">{{ t('taxes.amount') }}</th>
          </tr>
        </thead>
        <tbody>
          <template v-for="row in rows" :key="row.date">
            <!-- weekend days without an entry collapse into one thin row per weekend -->
            <tr v-if="row.collapsed" class="weekend-row">
              <td class="day-cell" @pointerdown="onDayPointerDown($event, row.date)">
                <span class="day-num">{{ row.day }}</span><span class="day-name">{{ row.dayName }}</span>
              </td>
              <td :colspan="(showAmount ? 5 : 4) + (showRate ? 1 : 0)">
                {{ row.label }} ·
                <button
                  v-for="d in row.markable"
                  :key="d.date"
                  type="button"
                  class="link-btn"
                  @click="setKind([d.date], DayType.Worked)"
                >
                  {{ t('time.markAsWorked', { day: d.label }) }}
                </button>
              </td>
            </tr>

            <tr v-else :class="{ selected: selected.includes(row.date), weekend: row.isWeekend }">
              <td
                class="day-cell"
                :title="t('time.dayCellHint')"
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
                  :aria-label="t('time.typeFor', { day: row.label })"
                  @change="onKindChange(row.date, ($event.target as HTMLSelectElement).value)"
                >
                  <option :value="String(DayType.Worked)">{{ t('time.kinds.worked') }}</option>
                  <option :value="String(DayType.PublicHoliday)">{{ t('time.kinds.publicHoliday') }}</option>
                  <option :value="String(DayType.UnpaidLeave)">{{ t('time.kinds.unpaidLeave') }}</option>
                  <option value="none">{{ t('time.kinds.notBilled') }}</option>
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
                  :title="isSplit(row.date) ? t('time.splitHoursHint') : ''"
                  :aria-label="t('time.hoursFor', { day: row.label })"
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
                    :title="t('time.editSplitHint')"
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
                    :placeholder="t('time.projectOptional')"
                    :value="get(row.date)?.projects?.[0]?.projectName ?? ''"
                    :aria-label="t('time.projectFor', { day: row.label })"
                    @change="setProject([row.date], ($event.target as HTMLInputElement).value.trim())"
                  />
                </template>
                <span v-else class="dash">—</span>
              </td>

              <td v-if="showRate">
                <select
                  v-if="kindOf(row.date) === DayType.Worked"
                  :id="`ts-rate-${row.date}`"
                  class="input rate-select"
                  :value="get(row.date)?.rateId != null ? String(get(row.date)?.rateId) : 'default'"
                  :aria-label="t('time.rateFor', { day: row.label })"
                  @change="onRateChange(row.date, ($event.target as HTMLSelectElement).value)"
                >
                  <option value="default">{{ t('time.invoiceRate') }}</option>
                  <option v-for="rate in rates" :key="rate.id" :value="String(rate.id)">{{ rateLabel(rate) }}</option>
                </select>
                <span v-else class="dash">—</span>
              </td>

              <td>
                <input
                  v-if="kindOf(row.date) !== 'none'"
                  :id="`ts-note-${row.date}`"
                  type="text"
                  maxlength="500"
                  class="input"
                  :placeholder="t('time.addNote')"
                  :value="get(row.date)?.notes ?? ''"
                  :aria-label="t('time.noteFor', { day: row.label })"
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
import { billedHours, type DayKind, type WorkMonth } from '@/composables/useWorkMonth'
import { dayAmount, rateLabel } from '@/utils/rates'
import type { RateDto, WorkDayDto } from '@/types'
import { currentLanguage } from '@/i18n'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const props = defineProps<{
  month: WorkMonth
  /** The rates a single day can choose from (empty or one: no choice, no column). */
  rates: RateDto[]
  /** The invoice's rate, used by days that don't choose one. */
  defaultRate: RateDto | null
  /** The rate a day is billed at. */
  rateOf: (day: WorkDayDto) => RateDto | null
  projectSuggestions: string[]
}>()

const {
  monthDates, selected, selectedDays, anchor, get, kindOf, select, clearSelection,
  setKind, setHours, setProject, setRate, setNote
} = props.month

// Sunday first, in the app's language (1 January 2023 was a Sunday)
const dayNames = computed(() => Array.from({ length: 7 }, (_, i) =>
  new Date(2023, 0, 1 + i).toLocaleDateString(currentLanguage(), { weekday: 'short' })))

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
      dayName: dayNames.value[dow],
      label: d.toLocaleDateString(currentLanguage(), { weekday: 'short', day: 'numeric', month: 'short' }),
      isWeekend
    }
    if (isWeekend && !get(date)) {
      // a Sunday already folded into the preceding Saturday row
      if (dow === 0 && i > 0 && result[result.length - 1]?.collapsed) continue
      const next = dates[i + 1]
      const foldSunday = dow === 6 && next && !get(next)
      const markable = [{ date, label: `${dayNames.value[dow]} ${d.getDate()}` }]
      if (foldSunday) markable.push({ date: next, label: `${dayNames.value[0]} ${d.getDate() + 1}` })
      result.push({ ...base, collapsed: true, markable, label: foldSunday ? t('time.weekend') : t('time.dayNotBilled', { day: dayNames.value[dow] }) })
      continue
    }
    result.push({ ...base, collapsed: false, markable: [] })
  }
  return result
})

const showAmount = computed(
  () => !!props.defaultRate && (props.defaultRate.type === RateType.Daily || props.defaultRate.type === RateType.Hourly)
)
const showRate = computed(() => props.rates.length > 1)

function onRateChange(date: string, value: string) {
  setRate([date], value === 'default' ? null : Number(value))
}

function hoursOf(date: string): number {
  const wd = get(date)
  return wd ? billedHours(wd) : 0
}

function isSplit(date: string): boolean {
  return (get(date)?.projects ?? []).length > 1
}

function splitLabel(date: string): string {
  return (get(date)?.projects ?? [])
    .map(p => `${p.projectName || t('time.unnamed')} ${Number(p.hours) || 0}h`)
    .join(' · ')
}

function amountOf(date: string): string {
  const wd = get(date)
  const rate = wd ? props.rateOf(wd) : null
  if (!wd || kindOf(date) !== DayType.Worked || !rate) return ''
  return `${dayAmount(rate, wd).toFixed(2)} ${rate.price.currency}`.trim()
}

function kindClass(date: string): string {
  const kind = kindOf(date)
  if (kind === DayType.Worked) return 'worked'
  if (kind === DayType.PublicHoliday) return 'holiday'
  if (kind === DayType.UnpaidLeave) return 'leave'
  return 'none'
}

function onKindChange(date: string, value: string) {
  const kind: DayKind = value === 'none' ? 'none' : (value as DayType)
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
  border: 1px solid var(--color-primary-line);
  border-radius: 0.5rem;
  background: var(--color-primary-soft);
  font-size: 0.85rem;
}

.bulk-bar strong {
  color: var(--color-primary);
}

.spacer {
  flex: 1;
}

.bulk-btn {
  padding: 0.2rem 0.6rem;
  border: 1px solid var(--color-border-strong);
  border-radius: 0.375rem;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font: inherit;
  font-size: 0.8rem;
  line-height: 1.3;
  cursor: pointer;
}

.bulk-btn:hover {
  border-color: var(--color-primary-line);
  color: var(--color-primary);
}

.bulk-btn.ghost {
  border-color: transparent;
  background: transparent;
}

.table-wrap {
  max-height: 34rem;
  overflow: auto;
  border: 1px solid var(--color-border);
  border-radius: 0.5rem;
  background: var(--color-surface);
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
  background: var(--color-surface-muted);
  border-bottom: 1px solid var(--color-border);
  text-align: left;
  font-size: 0.7rem;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--color-text-muted);
  white-space: nowrap;
}

td {
  padding: 0.25rem 0.5rem;
  border-bottom: 1px solid var(--color-border);
  vertical-align: middle;
}

.num {
  text-align: right;
}

tr.selected td {
  background: var(--color-primary-soft);
}

tr.weekend td {
  background: var(--color-surface-muted);
}

tr.selected.weekend td {
  background: var(--color-primary-soft);
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
  color: var(--color-text-muted);
}

.weekend-row td {
  padding-block: 0.2rem;
  background: var(--color-surface-muted);
  color: var(--color-text-subtle);
  font-size: 0.78rem;
}

.input {
  width: 100%;
  min-width: 0;
  padding: 0.25rem 0.45rem;
  border: 1px solid var(--color-border);
  border-radius: 0.3rem;
  background: var(--color-surface);
  font: inherit;
  font-size: 0.85rem;
}

.input:hover {
  border-color: var(--color-border-strong);
}

.input:focus {
  outline: none;
  border-color: var(--color-primary);
  box-shadow: var(--focus-ring);
}

.type-select {
  width: 8.75rem;
  font-weight: 500;
}

.type-select.worked { color: var(--color-success); }
.type-select.holiday { color: var(--color-warning); }
.type-select.leave { color: var(--color-danger); }
.type-select.none { color: var(--color-text-muted); }

.hours-input {
  width: 4.75rem;
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.hours-input[readonly] {
  background: var(--color-surface-muted);
  color: var(--color-text-muted);
}

.hours-input.invalid {
  border-color: var(--color-danger);
}

.split-chip {
  max-width: 16rem;
  padding: 0.2rem 0.5rem;
  border: 1px dashed var(--color-border-strong);
  border-radius: 0.3rem;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font: inherit;
  font-size: 0.78rem;
  line-height: 1.3;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  cursor: pointer;
}

.split-chip:hover {
  border-color: var(--color-primary);
}

.amount {
  white-space: nowrap;
  font-variant-numeric: tabular-nums;
  color: var(--color-text-secondary);
}

.rate-select {
  min-width: 9rem;
}

.dash {
  color: var(--color-border-strong);
}

.link-btn {
  padding: 0;
  margin-right: 0.5rem;
  border: 0;
  background: none;
  color: var(--color-primary);
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
