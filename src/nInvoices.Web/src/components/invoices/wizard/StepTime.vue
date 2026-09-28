<template>
  <BasePanel>
    <template #header>
      <h2 class="panel-heading">Worked days · {{ periodLabel }}</h2>
      <p class="panel-help">
        <template v-if="timeView === 'calendar'">Weekdays start as full days. Select the days that differ and edit them on the right.</template>
        <template v-else>Edit any day in its row. Click a day number to select it (Shift for a range) and split it across projects on the right.</template>
      </p>
      <p v-if="holidayCountryName" class="panel-help holidays">
        <AppIcon name="calendar" />
        <span v-if="holidays.length">
          Public holidays in {{ holidayCountryName }}:
          <template v-for="(holiday, index) in holidays" :key="holiday.date">{{ index ? ', ' : '' }}<strong>{{ dayLabel(holiday.date) }}</strong> {{ holiday.name }}</template>.
          Weekdays among them are marked as holidays.
        </span>
        <span v-else>No public holidays in {{ holidayCountryName }} this month.</span>
        <router-link to="/settings">Edit holidays</router-link>
      </p>
    </template>
    <template #actions>
      <div class="view-switch" role="group" aria-label="View">
        <button
          v-for="view in timeViews"
          :key="view.value"
          type="button"
          :class="{ active: timeView === view.value }"
          :aria-pressed="timeView === view.value"
          @click="timeView = view.value"
        >
          {{ view.label }}
        </button>
      </div>
      <BaseButton size="sm" @click="fillWeekdays">Fill weekdays</BaseButton>
      <BaseButton size="sm" variant="ghost" @click="workMonth.clearMonth">Clear</BaseButton>
    </template>

    <div v-if="customerRates.length > 1" class="rate-picker">
      <label for="time-rate" class="rate-label">Rate</label>
      <select id="time-rate" v-model="form.rateId" class="control">
        <option v-for="rate in customerRates" :key="rate.id" :value="rate.id">{{ rateLabel(rate) }}</option>
      </select>
      <span class="rate-help">
        <template v-if="canChooseDayRate">
          The invoice's rate: days without a rate of their own are billed at it. To bill a day at another rate, select it and pick the rate in the day panel<template v-if="timeView === 'list'"> or in the Rate column</template>.
        </template>
        <template v-else-if="selectedRate?.type === RateType.Monthly">A fixed monthly price: days aren't billed one by one.</template>
        <template v-else>The rate every worked day is billed at.</template>
      </span>
    </div>

    <div class="time-layout">
      <MonthCalendar v-if="timeView === 'calendar'" :month="workMonth" :rate-badge="rateBadge" />
      <TimesheetList
        v-else
        :month="workMonth"
        :rates="dayRateOptions"
        :default-rate="selectedRate"
        :rate-of="rateOfDay"
        :project-suggestions="projectSuggestions"
      />
      <DayInspector
        :month="workMonth"
        :rates="dayRateOptions"
        :default-rate="selectedRate"
        :rate-of="rateOfDay"
        :project-suggestions="projectSuggestions"
      />
    </div>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { InvoiceDraft } from '@/composables/useInvoiceDraft'
import MonthCalendar from '@/components/time/MonthCalendar.vue'
import DayInspector from '@/components/time/DayInspector.vue'
import TimesheetList from '@/components/time/TimesheetList.vue'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import { RateType } from '@/types'
import { rateLabel, rateShortLabel } from '@/utils/rates'

const props = defineProps<{ draft: InvoiceDraft }>()

const {
  form, workMonth, selectedRate, customerRates, dayRateOptions, canChooseDayRate, rateOfDay,
  projectSuggestions, periodLabel, holidays, holidayCountryName, fillWeekdays
} = props.draft

/** The calendar marks days billed at a rate other than the invoice's. */
function rateBadge(date: string): string {
  const day = workMonth.get(date)
  if (!day || !canChooseDayRate.value) return ''
  const rate = rateOfDay(day)
  return rate && rate.id !== selectedRate.value?.id ? rateShortLabel(rate) : ''
}

function dayLabel(date: string) {
  return new Date(`${date}T00:00:00`).toLocaleDateString(undefined, { weekday: 'short', day: 'numeric' })
}

// Calendar and List edit the same data; the chosen view is remembered per browser
type TimeView = 'calendar' | 'list'
const TIME_VIEW_KEY = 'ninvoices.timeView'
const timeViews: { value: TimeView; label: string }[] = [
  { value: 'calendar', label: 'Calendar' },
  { value: 'list', label: 'List' }
]
const timeView = ref<TimeView>(readTimeView())

function readTimeView(): TimeView {
  try {
    return localStorage.getItem(TIME_VIEW_KEY) === 'list' ? 'list' : 'calendar'
  } catch {
    return 'calendar'
  }
}

watch(timeView, view => {
  try {
    localStorage.setItem(TIME_VIEW_KEY, view)
  } catch {
    // storage unavailable (private mode): the choice just isn't remembered
  }
})
</script>

<style scoped>
.panel-heading {
  font-size: 1rem;
  font-weight: 600;
}

.panel-help {
  margin: 0.2rem 0 0;
  max-width: 70ch;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.rate-picker {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem 0.75rem;
  margin-bottom: 1rem;
  padding: 0.6rem 0.75rem;
  border-radius: var(--radius-md);
  background: var(--color-surface-muted);
}

.rate-label {
  font-weight: 600;
  color: var(--color-text);
}

.rate-picker .control {
  width: auto;
  min-width: 14rem;
}

.rate-help {
  flex: 1 1 18rem;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.holidays .app-icon {
  width: 0.9rem;
  height: 0.9rem;
  margin-right: 0.3rem;
  vertical-align: -0.12em;
  color: var(--color-primary);
}

.holidays a {
  margin-left: 0.35rem;
}

.holidays strong {
  color: var(--color-text-secondary);
  font-weight: 600;
}

.view-switch {
  display: inline-flex;
  gap: 2px;
  padding: 2px;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
}

.view-switch button {
  padding: 0.25rem 0.7rem;
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--text-sm);
}

.view-switch button:hover {
  background: var(--color-surface-sunken);
  color: var(--color-text);
}

.view-switch button.active {
  background: var(--color-primary);
  color: var(--color-on-primary);
  font-weight: 600;
}

.time-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 20rem;
  gap: 1rem;
  align-items: start;
}

@media (max-width: 1000px) {
  .time-layout {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
