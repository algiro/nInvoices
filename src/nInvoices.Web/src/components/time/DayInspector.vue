<template>
  <aside class="inspector" aria-label="Selected days">
    <div v-if="days.length === 0" class="empty">
      <strong>No day selected</strong>
      <span>Click a day to edit it, or drag across several days to change them together.</span>
    </div>

    <template v-else>
      <div class="section">
        <h4 class="title">{{ title }}</h4>
        <span class="subtitle">{{ subtitle }}</span>
      </div>

      <div class="section">
        <span class="label">Day type</span>
        <div class="segmented" role="group" aria-label="Day type">
          <button
            v-for="option in kindOptions"
            :key="String(option.value)"
            type="button"
            :class="['seg-btn', option.css, { active: commonKind === option.value }]"
            :aria-pressed="commonKind === option.value"
            @click="setKind(days, option.value)"
          >
            {{ option.label }}
          </button>
        </div>
      </div>

      <template v-if="commonKind === DayType.Worked">
        <div class="section">
          <span class="label">{{ days.length > 1 ? 'Hours (each day)' : 'Hours' }}</span>
          <div class="chips">
            <button
              v-for="preset in hourPresets"
              :key="preset.hours"
              type="button"
              class="chip"
              :class="{ active: commonHours === preset.hours }"
              @click="setHours(days, preset.hours)"
            >
              {{ preset.hours }}h <small>{{ preset.label }}</small>
            </button>
            <input
              id="inspector-hours"
              type="number"
              step="0.25"
              min="0"
              max="24"
              class="input hours-input"
              :value="commonHours ?? ''"
              :placeholder="commonHours === null ? 'mixed' : ''"
              aria-label="Custom hours"
              @change="setHours(days, Number(($event.target as HTMLInputElement).value))"
            />
          </div>
        </div>

        <div class="section">
          <span class="label">{{ days.length > 1 ? 'Project (all selected days)' : 'Projects' }}</span>

          <template v-if="singleDay">
            <div v-for="(alloc, index) in singleDay.projects ?? []" :key="index" class="alloc-row">
              <input
                :id="`inspector-project-${index}`"
                v-model.trim="alloc.projectName"
                type="text"
                list="inspector-project-suggestions"
                placeholder="Project name (optional)"
                class="input"
                aria-label="Project name"
              />
              <input
                :id="`inspector-project-hours-${index}`"
                v-model.number="alloc.hours"
                type="number"
                step="0.25"
                min="0"
                max="24"
                class="input alloc-hours"
                aria-label="Hours on this project"
              />
              <button
                type="button"
                class="icon-btn"
                title="Remove project"
                :disabled="(singleDay.projects ?? []).length < 2"
                @click="removeAllocation(singleDay.date, index)"
              >
                ✕
              </button>
            </div>
            <button type="button" class="link-btn" @click="addAllocation(singleDay.date)">+ Split with another project</button>
          </template>

          <input
            v-else
            id="inspector-project-all"
            type="text"
            list="inspector-project-suggestions"
            class="input"
            :value="commonProject ?? ''"
            :placeholder="commonProject === null ? 'Mixed projects' : 'Project name (optional)'"
            aria-label="Project for all selected days"
            @change="setProject(days, ($event.target as HTMLInputElement).value.trim())"
          />

          <datalist id="inspector-project-suggestions">
            <option v-for="p in projectSuggestions" :key="p" :value="p" />
          </datalist>
          <p v-if="requireHours && hasDayWithoutHours" class="error">Every worked day needs more than 0 hours.</p>
        </div>
      </template>

      <div v-if="commonKind !== 'none'" class="section">
        <label class="label" for="inspector-note">{{ days.length > 1 ? 'Note (applies to all)' : 'Note' }}</label>
        <textarea
          id="inspector-note"
          class="input note"
          maxlength="500"
          rows="2"
          :value="commonNote ?? ''"
          :placeholder="commonNote === null ? 'Different notes. Typing replaces all of them.' : 'Shown on the monthly report, e.g. “Half day, medical appointment”'"
          @input="setNote(days, ($event.target as HTMLTextAreaElement).value)"
        ></textarea>
      </div>

      <div v-if="commonKind === DayType.Worked && calculation" class="section">
        <div class="calc">{{ calculation }}</div>
      </div>
    </template>

    <div class="section shortcuts">
      <span><kbd>W</kbd> worked</span>
      <span><kbd>H</kbd> holiday</span>
      <span><kbd>L</kbd> leave</span>
      <span><kbd>Del</kbd> clear</span>
      <span><kbd>1</kbd>–<kbd>8</kbd> hours</span>
    </div>
  </aside>
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

const { selectedDays, get, kindOf, setKind, setHours, setProject, setNote, addAllocation, removeAllocation } = props.month

const kindOptions: { value: DayKind; label: string; css: string }[] = [
  { value: DayType.Worked, label: 'Worked', css: 'worked' },
  { value: DayType.PublicHoliday, label: 'Holiday', css: 'holiday' },
  { value: DayType.UnpaidLeave, label: 'Leave', css: 'leave' },
  { value: 'none', label: 'Not billed', css: 'none' }
]

const hourPresets = [
  { hours: 8, label: 'full' },
  { hours: 6, label: '¾' },
  { hours: 4, label: 'half' },
  { hours: 2, label: '¼' }
]

const days = computed(() => selectedDays.value)
const entries = computed(() => days.value.map(d => get(d)))
const singleDay = computed(() => (days.value.length === 1 ? entries.value[0] : undefined))

/** The value shared by every selected day, or null when they differ. */
function common<T>(values: T[]): T | null {
  return values.length > 0 && values.every(v => v === values[0]) ? values[0] : null
}

const commonKind = computed(() => common(days.value.map(d => kindOf(d))))
const commonHours = computed(() => common(entries.value.map(wd => (wd ? billedHours(wd) : 0))))
const commonNote = computed(() => common(entries.value.map(wd => wd?.notes ?? '')))
const commonProject = computed(() =>
  common(entries.value.map(wd => ((wd?.projects ?? []).length === 1 ? wd!.projects![0].projectName : '\u0000')))
)

const requireHours = computed(() => props.rateType === RateType.Hourly || props.rateType === RateType.Daily)
const hasDayWithoutHours = computed(() =>
  entries.value.some(wd => wd && (props.rateType === RateType.Hourly ? dayHours(wd) : billedHours(wd)) <= 0)
)

const dateFormat = (date: string, opts: Intl.DateTimeFormatOptions) =>
  new Date(date + 'T00:00:00').toLocaleDateString(undefined, opts)

const title = computed(() =>
  days.value.length === 1
    ? dateFormat(days.value[0], { weekday: 'long', day: 'numeric', month: 'long' })
    : `${days.value.length} days selected`
)

const subtitle = computed(() => {
  if (days.value.length === 1) {
    const dow = new Date(days.value[0] + 'T00:00:00').getDay()
    return dow === 0 || dow === 6 ? 'Weekend' : 'Weekday'
  }
  const first = days.value[0]
  const last = days.value[days.value.length - 1]
  const contiguous = (new Date(last).getTime() - new Date(first).getTime()) / 86_400_000 + 1 === days.value.length
  const range = contiguous
    ? `${dateFormat(first, { day: 'numeric', month: 'short' })} – ${dateFormat(last, { day: 'numeric', month: 'short' })}`
    : days.value.map(d => dateFormat(d, { day: 'numeric' })).join(', ')
  return commonKind.value === null ? `${range} · mixed types` : range
})

const calculation = computed(() => {
  if (!props.rateAmount || !props.currency) return ''
  const hours = entries.value.reduce((sum, wd) => sum + (wd ? billedHours(wd) : 0), 0)
  const money = (v: number) => `${v.toFixed(2)} ${props.currency}`
  if (props.rateType === RateType.Daily) {
    const effective = hours / FULL_DAY_HOURS
    return `${hours}h ÷ ${FULL_DAY_HOURS} = ${effective.toFixed(2)} day(s) × ${money(props.rateAmount)} = ${money(effective * props.rateAmount)}`
  }
  if (props.rateType === RateType.Hourly) {
    return `${hours}h × ${money(props.rateAmount)} = ${money(hours * props.rateAmount)}`
  }
  return ''
})
</script>

<style scoped>
.inspector {
  display: flex;
  flex-direction: column;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 0.5rem;
  position: sticky;
  top: 1rem;
}

.section {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.75rem 0.875rem;
  border-bottom: 1px solid var(--color-border);
}

.section:last-child {
  border-bottom: 0;
}

.empty {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  padding: 1.5rem 1rem;
  text-align: center;
  color: var(--color-text-muted);
  font-size: 0.875rem;
  border-bottom: 1px solid var(--color-border);
}

.empty strong {
  color: var(--color-text);
}

.title {
  margin: 0;
  font-size: 1rem;
  font-weight: 600;
  color: var(--color-text);
}

.subtitle {
  font-size: 0.8rem;
  color: var(--color-text-muted);
}

.label {
  font-size: 0.7rem;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

/* explicit button styling: the global stylesheet styles every <button> */
.seg-btn,
.chip,
.icon-btn,
.link-btn {
  font-family: inherit;
  line-height: 1.2;
  transition: background 0.12s, border-color 0.12s, color 0.12s;
}

.segmented {
  display: flex;
  gap: 2px;
  padding: 2px;
  border: 1px solid var(--color-border-strong);
  border-radius: 0.4rem;
  background: var(--color-surface);
}

.seg-btn {
  flex: 1;
  padding: 0.35rem 0.25rem;
  border: 0;
  border-radius: 0.3rem;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 0.8rem;
  cursor: pointer;
  white-space: nowrap;
}

.seg-btn:hover {
  background: var(--color-surface-sunken);
}

.seg-btn.active {
  color: var(--color-on-status);
  font-weight: 600;
}

.seg-btn.active.worked { background: var(--color-success); }
.seg-btn.active.holiday { background: var(--color-warning); }
.seg-btn.active.leave { background: var(--color-danger); }
.seg-btn.active.none { background: var(--color-text-secondary); }

.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.375rem;
  align-items: center;
}

.chip {
  padding: 0.25rem 0.5rem;
  border: 1px solid var(--color-border-strong);
  border-radius: 0.375rem;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-size: 0.8rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  cursor: pointer;
}

.chip small {
  font-weight: 400;
  color: var(--color-text-muted);
}

.chip:hover {
  border-color: var(--color-primary-line);
}

.chip.active {
  background: var(--color-primary-soft);
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.input {
  width: 100%;
  min-width: 0;
  padding: 0.375rem 0.5rem;
  border: 1px solid var(--color-border-strong);
  border-radius: 0.375rem;
  font: inherit;
  font-size: 0.875rem;
  background: var(--color-surface);
}

.input:focus {
  outline: none;
  border-color: var(--color-primary);
  box-shadow: var(--focus-ring);
}

.hours-input {
  width: 4.5rem;
  text-align: right;
}

.alloc-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 4.5rem 1.75rem;
  gap: 0.375rem;
  align-items: center;
}

.alloc-hours {
  text-align: right;
}

.icon-btn {
  height: 1.75rem;
  padding: 0;
  border: 0;
  border-radius: 0.25rem;
  background: transparent;
  color: var(--color-text-subtle);
  cursor: pointer;
}

.icon-btn:hover:not(:disabled) {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}

.icon-btn:disabled {
  visibility: hidden;
}

.link-btn {
  align-self: flex-start;
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-primary);
  font-size: 0.8rem;
  font-weight: 500;
  cursor: pointer;
}

.link-btn:hover {
  text-decoration: underline;
}

.note {
  resize: vertical;
  min-height: 3.25rem;
}

.error {
  margin: 0;
  font-size: 0.8rem;
  color: var(--color-danger);
}

.calc {
  padding: 0.4rem 0.5rem;
  border: 1px solid var(--color-border);
  border-radius: 0.375rem;
  background: var(--color-surface-muted);
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
}

.shortcuts {
  flex-direction: row;
  flex-wrap: wrap;
  gap: 0.25rem 0.75rem;
  font-size: 0.72rem;
  color: var(--color-text-muted);
}

kbd {
  font-family: ui-monospace, 'Cascadia Mono', Consolas, monospace;
  font-size: 0.68rem;
  padding: 0 0.3rem;
  border: 1px solid var(--color-border-strong);
  border-bottom-width: 2px;
  border-radius: 0.25rem;
  background: var(--color-surface);
  color: var(--color-text-secondary);
}
</style>
